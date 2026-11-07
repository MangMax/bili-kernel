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
    private readonly BuvidManager _buvidManager;

    public LiveDanmakuApiClient(
        BiliHttpClient httpClient,
        BiliAuthenticator authenticator,
        IBiliTokenResolver tokenResolver,
        BuvidManager buvidManager)
    {
        _httpClient = httpClient;
        _authenticator = authenticator;
        _tokenResolver = tokenResolver;
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

            var port = host.WssPort > 0 ? host.WssPort : host.Port > 0 ? host.Port : 443;
            hosts.Add(new LiveHost(host.Host, port));
        }

        if (hosts.Count == 0)
        {
            throw new KernelException("无法获取直播弹幕服务器地址");
        }

        var buvid = _buvidManager.Buvid3;
        if (string.IsNullOrEmpty(buvid))
        {
            buvid = await GetBuvidAsync(cancellationToken).ConfigureAwait(false);
        }

        return new LiveDanmakuEndpoint
        {
            RoomId = realRoomId,
            UserId = _tokenResolver.GetToken()?.UserId ?? 0,
            Token = data.Token ?? string.Empty,
            Buvid = buvid,
            Hosts = hosts,
        };
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

    private async Task<string> GetBuvidAsync(CancellationToken cancellationToken)
    {
        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(BiliApis.Account.Buvid));
        _authenticator.AuthorizeRestRequest(request, default, new BiliAuthorizeExecutionSettings { RequireCookie = true });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseBuvidResponse).ConfigureAwait(false);
        return responseObj?.Data?.B3 ?? string.Empty;
    }
}
