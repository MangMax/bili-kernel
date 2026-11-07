// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using RichasyKernel;
using System.Text;
using System.Text.Json;

namespace Richasy.BiliKernel.Authenticator;

/// <summary>
/// 授权器，用于对请求进行授权.
/// </summary>
public sealed partial class BiliAuthenticator
{
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Lock _wbiGate = new();
    private Task? _wbiRefresh;

    /// <summary>
    /// Initializes a new instance of the <see cref="BiliAuthenticator"/> class.
    /// </summary>
    public BiliAuthenticator(
        IBiliCookiesResolver? localCookiesResolver,
        IBiliTokenResolver? localTokenResolver,
        BuvidManager? buvidManager = null,
        BiliHttpClient? httpClient = null)
        : this(localCookiesResolver, localTokenResolver, buvidManager, httpClient, () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>创建使用指定 UTC 时钟的授权器，缓存作用域为该实例。</summary>
    public BiliAuthenticator(
        IBiliCookiesResolver? localCookiesResolver,
        IBiliTokenResolver? localTokenResolver,
        BuvidManager? buvidManager,
        BiliHttpClient? httpClient,
        Func<DateTimeOffset> utcNow)
    {
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        _cookieResolver = localCookiesResolver;
        _tokenResolver = localTokenResolver;
        _buvidManager = buvidManager;
        _httpClient = httpClient;
    }

    /// <summary>
    /// 对 REST Api 请求进行授权.
    /// </summary>
    /// <param name="request">请求.</param>
    /// <param name="parameters">参数.</param>
    /// <param name="settings">请求设置.</param>
    public void AuthorizeRestRequest(HttpRequestMessage request, Dictionary<string, string>? parameters = default, BiliAuthorizeExecutionSettings? settings = default)
    {
        Verify.NotNull(request, nameof(request));
        var executionSettings = settings ?? new BiliAuthorizeExecutionSettings();
        if (executionSettings.RequireCookie && _cookieResolver != null)
        {
            var cookies = _cookieResolver.GetCookies();
            if (cookies?.Count > 0)
            {
                request.Headers.Add("Cookie", string.Join("; ", cookies.Select(p => $"{p.Key}={p.Value}")));
            }
        }

        if (request.Method == HttpMethod.Post)
        {
            var queryParameters = AuthorizeRequestParameters(parameters, executionSettings);
            request.Content = new FormUrlEncodedContent(queryParameters);
        }
        else
        {
            var uri = request.RequestUri;
            var query = GenerateAuthorizedQueryString(parameters, executionSettings);
            uri = new UriBuilder(uri) { Query = query }.Uri;
            request.RequestUri = uri;
        }
    }

    /// <summary>
    /// 对 gRPC 请求进行授权.
    /// </summary>
    public void AuthorizeGrpcRequest(HttpRequestMessage request, bool requireToken = false)
    {
        Verify.NotNull(request);
        var token = _tokenResolver?.GetToken();
        if (requireToken && token == null)
        {
            throw new KernelException("需要令牌，但令牌不存在，请重新登录");
        }

        var accessToken = token?.AccessToken ?? string.Empty;
        var grpcConfig = new GrpcConfig(accessToken);
        var userAgent = BiliClientIdentity.GrpcUserAgent;

        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Add("authorization", $"identify_v1 {accessToken}");
            request.Headers.Add("x-bili-mid", token.UserId.ToString());
        }

        request.Headers.Add("user-agent", userAgent);
        request.Headers.Add("x-bili-device-bin", GrpcConfig.GetDeviceBin());
        request.Headers.Add("x-bili-fawkes-req-bin", GrpcConfig.GetFawkesreqBin());
        request.Headers.Add("x-bili-locale-bin", GrpcConfig.GetLocaleBin());
        request.Headers.Add("x-bili-metadata-bin", grpcConfig.GetMetadataBin());
        request.Headers.Add("x-bili-network-bin", GrpcConfig.GetNetworkBin());
        request.Headers.Add("x-bili-restriction-bin", GrpcConfig.GetRestrictionBin());
        request.Headers.Add("grpc-accept-encoding", "identity,deflate,gzip");
        request.Headers.Add("grpc-timeout", "20100m");
        request.Headers.Add("env", GrpcConfig.Envorienment);
        request.Headers.Add("Transfer-Encoding", "chunked");
        request.Headers.Add("TE", "trailers");
        request.Headers.Add("x-bili-aurora-eid", GrpcConfig.GetAuroraEid(token?.UserId ?? 0));
        request.Headers.Add("x-bili-trace-id", GrpcConfig.GetTraceId());
        request.Headers.Add("buvid", _buvidManager?.Buvid3 ?? GetBuvid());
    }

    /// <summary>
    /// 生成授权后的请求参数.
    /// </summary>
    /// <param name="parameters">基础参数.</param>
    /// <param name="settings">授权设置.</param>
    /// <returns>处理后的参数列表.</returns>
    /// <exception cref="KernelException">令牌获取失败.</exception>
    public Dictionary<string, string> AuthorizeRequestParameters(Dictionary<string, string>? parameters, BiliAuthorizeExecutionSettings? settings = default)
    {
        var queryParameters = parameters ?? new Dictionary<string, string>();
        var executionSettings = settings ?? new BiliAuthorizeExecutionSettings();
        if (executionSettings.NeedCSRF && !queryParameters.ContainsKey("csrf"))
        {
            queryParameters.Add("csrf", GetCsrfToken());
        }

        if (!executionSettings.SkipBuild && !queryParameters.ContainsKey("build"))
        {
            queryParameters.Add("build", BuildNumber);
        }

        InitializeDeviceParameters(queryParameters, executionSettings.ApiType, executionSettings.OnlyUseAppKey);

        if (!executionSettings.ForceNoToken)
        {
            var token = _tokenResolver?.GetToken();
            if (token == null && executionSettings.RequireToken)
            {
                throw new KernelException("需要令牌，但令牌不存在，请重新登录");
            }

            var accessToken = token?.AccessToken ?? string.Empty;
            if (!string.IsNullOrEmpty(accessToken))
            {
                queryParameters.Add("access_key", accessToken);
            }
        }

        if (executionSettings.NeedRID)
        {
            FilterWbiIllegalCharacters(queryParameters);
            var rid = GenerateRID(queryParameters);
            queryParameters.Add("w_rid", rid);
        }
        else
        {
            var sign = GenerateSign(queryParameters, executionSettings.ApiType);
            queryParameters.Add("sign", sign);
        }

        return queryParameters;
    }

    /// <summary>
    /// 生成授权后的查询字符串.
    /// </summary>
    /// <param name="parameters">基础参数.</param>
    /// <param name="settings">授权设置.</param>
    /// <returns>字符串.</returns>
    public string GenerateAuthorizedQueryString(Dictionary<string, string>? parameters, BiliAuthorizeExecutionSettings? settings = default)
    {
        var queryParameters = AuthorizeRequestParameters(parameters, settings);
        return GenerateQuery(queryParameters);
    }

    /// <summary>
    /// 初始化或刷新实例的 WBI 密钥，成功后缓存 24 小时；同实例并发请求合并。
    /// 刷新发起者取消会取消本轮刷新；等待者取消仅结束自身等待。
    /// 失败或取消后由后续显式调用重试，不使用过期密钥。
    /// </summary>
    public async Task InitializeWbiAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task refresh;
        var ownsRefresh = false;
        lock (_wbiGate)
        {
            if (_wbiCache != null && _utcNow() < _wbiCache.Item2)
            {
                return;
            }

            if (_wbiRefresh == null || _wbiRefresh.IsCompleted)
            {
                ownsRefresh = true;
                _wbiRefresh = RefreshWbiAsync(cancellationToken);
            }

            refresh = _wbiRefresh;
        }

        if (ownsRefresh)
        {
            // Complete cancellation cleanup before a subsequent explicit call can retry.
            await refresh.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        // A waiting caller may cancel without cancelling another caller's refresh.
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
        {
            await Task.WhenAny(refresh, cancelled.Task).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await refresh.ConfigureAwait(false);
        }
    }

    private async Task RefreshWbiAsync(CancellationToken cancellationToken)
    {
        if (_wbiCache != null && _utcNow() < _wbiCache.Item2)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, BiliApis.Passport.WebNav);
        request.Headers.Add("User-Agent", BiliClientIdentity.WebUserAgent);
        var cookieString = _cookieResolver?.GetCookieString();
        if (!string.IsNullOrEmpty(cookieString))
        {
            request.Headers.Add("Cookie", cookieString);
        }

        string responseText;
        if (_httpClient != null)
        {
            responseText = await _httpClient.GetStringAsync(request, cancellationToken, ensureSuccess: true).ConfigureAwait(false);
        }
        else
        {
            using var client = new HttpClient(new HttpClientHandler
            {
                UseCookies = true,
            });
            using var response = await client.SendAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        var result = JsonSerializer.Deserialize(responseText, SourceGenerationContext.Default.BiliDataResponseWebNavResponse);
        var img = result?.Data?.Img?.ImgUrl ?? string.Empty;
        var sub = result?.Data?.Img?.SubUrl ?? string.Empty;
        if (string.IsNullOrEmpty(img) || string.IsNullOrEmpty(sub))
        {
            throw new InvalidOperationException("WBI response did not contain valid keys.");
        }

        var key = Path.GetFileNameWithoutExtension(img) + Path.GetFileNameWithoutExtension(sub);
        if (key.Length < 64)
        {
            throw new InvalidOperationException("WBI response did not contain valid keys.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        _wbiCache = Tuple.Create(GenerateWbi(key), _utcNow().AddHours(24));

        string GenerateWbi(string key)
        {
            var binding = new List<byte>();
            var rawbiKey = Encoding.UTF8.GetBytes(key);
            foreach (var b in MIXIN_KEY_ENC_TAB)
            {
                binding.Add(rawbiKey[b]);
            }

            var mixinKey = Encoding.UTF8.GetString(binding.ToArray());
            return mixinKey.Substring(0, 32);
        }
    }

    private string GetCsrfToken()
    {
        if (_cookieResolver == null)
        {
            return string.Empty;
        }

        var cookies = _cookieResolver.GetCookies();
        return cookies != null && cookies.TryGetValue("bili_jct", out var token)
            ? token
            : string.Empty;
    }
}
