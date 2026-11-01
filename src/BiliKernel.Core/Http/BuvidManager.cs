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
    {
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
    public string? Buvid4 => string.IsNullOrEmpty(_buvid4) ? _store.Buvid4 : _buvid4;

    /// <summary>
    /// 确保设备标识可用。获取或激活失败只记录日志，不阻断业务请求。
    /// </summary>
    /// <param name="cancellationToken">终止令牌。</param>
    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(Buvid3))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrEmpty(Buvid3))
            {
                return;
            }

            await FetchAsync(cancellationToken).ConfigureAwait(false);
        }
        // 设备标识属于尽力而为的身份信息，失败不应阻断业务请求.
        catch (Exception ex)
        {
            Debug.WriteLine($"获取设备标识失败：{ex.Message}");
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
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var result = JsonSerializer.Deserialize(content, SourceGenerationContext.Default.BiliDataResponseBuvidResponse);
            var data = result?.Data;
            if (data == null || string.IsNullOrEmpty(data.B3))
            {
                return;
            }

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

        _activated = true;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BiliApis.Account.BuvidActivate))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "payload", CreateActivationPayload() },
                }),
            };
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            Debug.WriteLine($"设备标识激活结果：{(int)response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"激活设备标识失败：{ex.Message}");
        }
        catch (OperationCanceledException ex)
        {
            Debug.WriteLine($"激活设备标识被取消：{ex.Message}");
        }
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