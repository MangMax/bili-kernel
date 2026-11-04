// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Richasy.BiliKernel.Http;

/// <summary>
/// 设备标识管理器。在首次需要时获取 BUVID，写入 Cookie 并完成一次激活。
/// </summary>
public sealed class BuvidManager : IDisposable
{
    private readonly IBiliCookiesResolver? _cookiesResolver;
    private readonly IBuvidStore _store;
    private readonly HttpClient _client;
    private readonly bool _disposeClient;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _buvid3;
    private string? _buvid4;
    private bool _activated;
    private DateTimeOffset _retryAfter;
    private readonly Func<DateTimeOffset> _utcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuvidManager"/> class.
    /// </summary>
    /// <param name="cookiesResolver">Cookie 解析器。</param>
    /// <param name="store">设备标识存储，默认为本地文件存储。</param>
    /// <param name="messageHandler">用于获取设备标识的消息处理器，默认为真实网络。</param>
    public BuvidManager(
        IBiliCookiesResolver? cookiesResolver = null,
        IBuvidStore? store = null,
        HttpMessageHandler? messageHandler = null)
        : this(cookiesResolver, store, messageHandler, () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>创建使用指定 UTC 时钟的设备管理器。</summary>
    public BuvidManager(
        IBiliCookiesResolver? cookiesResolver,
        IBuvidStore? store,
        HttpMessageHandler? messageHandler,
        Func<DateTimeOffset> utcNow)
    {
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        _cookiesResolver = cookiesResolver;
        _store = store ?? new FileBuvidStore();
        _disposeClient = messageHandler == null;
        _client = messageHandler == null
            ? new HttpClient(new HttpClientHandler(), disposeHandler: true)
            : new HttpClient(messageHandler, disposeHandler: false);
        _client.DefaultRequestHeaders.Add("User-Agent", BiliClientIdentity.WebUserAgent);
        _client.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 获取 BUVID3。
    /// </summary>
    public string? Buvid3 => string.IsNullOrEmpty(_buvid3) ? _store.Buvid3 : _buvid3;

    /// <summary>
    /// 获取 BUVID4。
    /// </summary>
    public string? Buvid4 => string.IsNullOrEmpty(_buvid3) ? _store.Buvid4 : _buvid4;

    /// <summary>
    /// 确保设备标识可用。失败不阻断业务；一分钟冷却后由显式调用重试。
    /// 调用者取消会传播，不标记成功或进入冷却。成功激活在该管理器生命周期内只执行一次。
    /// </summary>
    /// <param name="cancellationToken">终止令牌。</param>
    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_activated || _utcNow() < _retryAfter)
            {
                return;
            }

            var cookies = _cookiesResolver?.GetCookies();
            if (string.IsNullOrEmpty(_buvid3) && cookies != null
                && cookies.TryGetValue("buvid3", out var cookieBuvid3) && !string.IsNullOrEmpty(cookieBuvid3))
            {
                // The caller's current Cookie identity takes precedence over stored fallback data.
                await FetchAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (!string.IsNullOrEmpty(Buvid3))
            {
                var buvid4 = Buvid4;
                _buvid3 = Buvid3;
                _buvid4 = buvid4;
                SaveToCookies();
                await ActivateAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await FetchAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        // 设备标识属于尽力而为的身份信息，失败不应阻断业务请求.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _retryAfter = _utcNow().AddMinutes(1);
            Debug.WriteLine("设备初始化失败；一分钟后允许显式调用重试。");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 把设备标识写入请求的 Cookie。
    /// </summary>
    /// <param name="request">请求。</param>
    public void ApplyToRequest(HttpRequestMessage request)
    {
        Verify.NotNull(request, nameof(request));
        var buvid3 = Buvid3;
        if (string.IsNullOrEmpty(buvid3))
        {
            return;
        }

        var cookie = request.Headers.TryGetValues("Cookie", out var values)
            ? string.Join("; ", values)
            : string.Empty;

        if (cookie.Contains("buvid3=", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(cookie))
        {
            parts.Add(cookie);
        }

        parts.Add($"buvid3={buvid3}");
        var buvid4 = Buvid4;
        if (!string.IsNullOrEmpty(buvid4))
        {
            parts.Add($"buvid4={buvid4}");
        }

        _ = request.Headers.Remove("Cookie");
        request.Headers.Add("Cookie", string.Join("; ", parts));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _gate.Dispose();
        if (_disposeClient)
        {
            _client.Dispose();
        }
    }

    private async Task FetchAsync(CancellationToken cancellationToken)
    {
        var cookies = _cookiesResolver?.GetCookies();
        if (cookies != null && cookies.TryGetValue("buvid3", out var cookieBuvid3) && !string.IsNullOrEmpty(cookieBuvid3))
        {
            _buvid3 = cookieBuvid3;
            _buvid4 = cookies.TryGetValue("buvid4", out var cookieBuvid4) ? cookieBuvid4 : null;
        }
        else
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BiliApis.Account.Buvid));
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var result = JsonSerializer.Deserialize(content, SourceGenerationContext.Default.BiliDataResponseBuvidResponse);
            var data = result?.Data;
            if (result?.Code != 0 || data == null || string.IsNullOrEmpty(data.B3))
            {
                throw new InvalidOperationException("Device registration failed.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            _buvid3 = data.B3;
            _buvid4 = data.B4;
            _store.Save(data.B3!, data.B4 ?? string.Empty);
        }

        SaveToCookies();
        await ActivateAsync(cancellationToken).ConfigureAwait(false);
    }

    private void SaveToCookies()
    {
        if (_cookiesResolver == null || string.IsNullOrEmpty(_buvid3))
        {
            return;
        }

        try
        {
            var cookies = _cookiesResolver.GetCookies() ?? new Dictionary<string, string>(StringComparer.Ordinal);
            cookies["buvid3"] = _buvid3!;
            if (!string.IsNullOrEmpty(_buvid4))
            {
                cookies["buvid4"] = _buvid4!;
            }

            _cookiesResolver.SaveCookies(cookies);
        }
        catch (NotSupportedException ex)
        {
            Debug.WriteLine($"写入设备标识 Cookie 失败：{ex.Message}");
        }
    }

    private async Task ActivateAsync(CancellationToken cancellationToken)
    {
        if (_activated || string.IsNullOrEmpty(Buvid3))
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BiliApis.Account.BuvidActivate))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "payload", CreateActivationPayload() },
            }),
        };
        var cookie = _cookiesResolver?.GetCookieString();
        if (!string.IsNullOrEmpty(cookie))
        {
            request.Headers.Add("Cookie", cookie);
        }

        ApplyToRequest(request);
        using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var document = JsonDocument.Parse(content);
        if (!document.RootElement.TryGetProperty("code", out var code) || code.GetInt32() != 0)
        {
            throw new InvalidOperationException("Device activation failed.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        _activated = true;
    }

    private static string CreateActivationPayload()
    {
        var buffer = new byte[32];
        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(buffer);
        }

        var bytes = new List<byte>(buffer);
        while (bytes.Count > 1 && bytes[0] == 0)
        {
            bytes.RemoveAt(0);
        }

        var chunks = new List<string>();
        var index = bytes.Count;
        while (index > 0)
        {
            var take = Math.Min(3, index);
            var value = 0L;
            for (var i = 0; i < take; i++)
            {
                value = (value << 8) | bytes[index - take + i];
            }

            chunks.Add(value.ToString("x6", CultureInfo.InvariantCulture));
            index -= take;
        }

        chunks.Reverse();
        var pngEnd = string.Concat(chunks);
        return $@"{{""3064"":1,""39c8"":""333.1387.fp.risk"",""3c43"":{{""adca"":""Linux"",""bfe9"":""{pngEnd}""}}}}";
    }
}
