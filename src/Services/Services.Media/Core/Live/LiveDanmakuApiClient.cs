// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using RichasyKernel;
using System.Globalization;
using System.Text.Json;

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 直播弹幕连接前置接口：短号归一化、getDanmuInfo、buvid 与登录态.
/// </summary>
internal sealed class LiveDanmakuApiClient
{
    private readonly BiliHttpClient _httpClient;
    private readonly BiliAuthenticator _authenticator;
    private readonly IBiliTokenResolver _tokenResolver;
    private readonly IBiliCookiesResolver _cookiesResolver;
    private readonly BuvidManager _buvidManager;

    public LiveDanmakuApiClient(
        BiliHttpClient httpClient,
        BiliAuthenticator authenticator,
        IBiliTokenResolver tokenResolver,
        IBiliCookiesResolver cookiesResolver,
        BuvidManager buvidManager)
    {
        _httpClient = httpClient;
        _authenticator = authenticator;
        _tokenResolver = tokenResolver;
        _cookiesResolver = cookiesResolver;
        _buvidManager = buvidManager;
    }

    public async Task<LiveDanmakuEndpoint> ResolveEndpointAsync(string roomId, CancellationToken cancellationToken)
    {
        var realRoomId = await GetRealRoomIdAsync(roomId, cancellationToken).ConfigureAwait(false);
        await _authenticator.InitializeWbiAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new Dictionary<string, string>
        {
            { "id", realRoomId },
            { "type", "0" },
            { "web_location", "444.8" },
        };

        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(BiliApis.Live.WebDanmakuInformation));
        _authenticator.AuthorizeRestRequest(request, parameters, BiliAuthorizeExecutionSettings.CookieOnly(needRID: true));
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseLiveDanmakuResponse).ConfigureAwait(false);
        var data = responseObj?.Data ?? throw new KernelException("无法获取直播弹幕信息");
        var hosts = new List<LiveHost>();
        foreach (var host in data.HostList ?? [])
        {
            if (string.IsNullOrWhiteSpace(host.Host))
            {
                continue;
            }

            // wss 走默认 443；接口给的 wss_port 是普通 ws 端口，不能用于 wss.
            hosts.Add(new LiveHost(host.Host, 443));
        }

        if (hosts.Count == 0)
        {
            throw new KernelException("无法获取直播弹幕服务器地址");
        }

        // buvid 优先取 Cookie；缺失时由 BuvidManager 走 finger/spi 回退并持久化，
        // 否则游客换设备后每次都要重新拿标识.
        await _buvidManager.EnsureAsync(cancellationToken).ConfigureAwait(false);
        var buvid = _buvidManager.Buvid3 ?? string.Empty;

        return new LiveDanmakuEndpoint
        {
            RoomId = realRoomId,
            UserId = ResolveUserId(),
            Token = data.Token ?? string.Empty,
            Buvid = buvid,
            Hosts = hosts,
        };
    }

    /// <summary>
    /// 弹幕连接的用户身份：优先令牌，其次 DedeUserID Cookie。
    /// 只挂 Cookie 登录（网页扫码）时令牌为空，若直接用 0 会成为游客，服务端不下发荣耀等级等字段。
    /// </summary>
    private long ResolveUserId()
    {
        var tokenUserId = _tokenResolver.GetToken()?.UserId ?? 0;
        if (tokenUserId > 0)
        {
            return tokenUserId;
        }

        var cookies = _cookiesResolver.GetCookies();
        return cookies.TryGetValue("DedeUserID", out var raw)
            && long.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var cookieUserId)
            && cookieUserId > 0
                ? cookieUserId
                : 0;
    }

    private async Task<string> GetRealRoomIdAsync(string roomId, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = new Dictionary<string, string> { { "id", roomId } };
            var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(BiliApis.Live.RoomInit));
            _authenticator.AuthorizeRestRequest(request, parameters, BiliAuthorizeExecutionSettings.CookieOnly());
            var text = await _httpClient.GetStringAsync(request, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("room_id", out var id)
                && id.TryGetInt64(out var value)
                && value > 0)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is KernelException or JsonException)
        {
            // 短号归一化失败时仍按原入参尝试；直接传真实房间号也能继续.
        }

        return roomId;
    }

}