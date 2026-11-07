// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Content;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media.Core.Models;
using RichasyKernel;
using System.Text.Json;

namespace Richasy.BiliKernel.Services.Media.Core;

internal sealed class LiveDiscoveryClient
{
    private static readonly int[] RiskControlCodes = [-352, -412, -509, 22015];

    private readonly BiliHttpClient _httpClient;
    private readonly BiliAuthenticator _authenticator;
    private readonly IBiliTokenResolver _tokenResolver;
    private readonly IBiliCookiesResolver? _cookiesResolver;

    public LiveDiscoveryClient(
        BiliHttpClient httpClient,
        BiliAuthenticator authenticator,
        IBiliTokenResolver tokenResolver,
        IBiliCookiesResolver? cookiesResolver = null)
    {
        _httpClient = httpClient;
        _authenticator = authenticator;
        _tokenResolver = tokenResolver;
        _cookiesResolver = cookiesResolver;
    }

    public async Task<IReadOnlyList<Partition>> GetLivePartitionsAsync(CancellationToken cancellationToken)
    {
        var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Get, new Uri(BiliApis.Live.LiveArea));
        _authenticator.AuthorizeRestRequest(request, settings: new BiliAuthorizeExecutionSettings { RequireToken = false });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseLiveAreaResponse).ConfigureAwait(false);
        return responseObj.Data is null || responseObj.Data.List.Count == 0
            ? throw new KernelException("直播分区数据为空")
            : (IReadOnlyList<Partition>)responseObj.Data.List.Select(p => p.ToPartition()).ToList().AsReadOnly();
    }

    public async Task<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber)> GetPartitionDetailAsync(Partition partition, LiveTag? tag = default, int pageNumber = 0, CancellationToken cancellationToken = default)
    {
        BiliDataResponse<LiveSecondListResponse>? appResponse = null;
        try
        {
            appResponse = await RequestAppAreaRoomsAsync(partition, tag, pageNumber, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException) when (HasLoginCookie())
        {
            // 传输层失败且有登录态时，单次回退 Web 端.
        }
        catch (HttpRequestException ex)
        {
            throw new KernelException($"获取直播分区列表失败：{ex.Message}", ex);
        }

        if (appResponse is not null)
        {
            if (appResponse.Code == 0)
            {
                return MapSecondList(appResponse, tag, pageNumber);
            }

            // 只有风控码且存在登录态 Cookie 时才回退，其余错误不掩盖.
            if (!RiskControlCodes.Contains(appResponse.Code) || !HasLoginCookie())
            {
                throw CreateFailure(appResponse.Code, appResponse.Message);
            }
        }

        BiliDataResponse<LiveSecondListResponse> webResponse;
        try
        {
            webResponse = await RequestWebAreaRoomsAsync(partition, tag, pageNumber, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new KernelException($"获取直播分区列表失败：{ex.Message}", ex);
        }

        return webResponse.Code == 0
            ? MapSecondList(webResponse, tag, pageNumber)
            : throw CreateFailure(webResponse.Code, webResponse.Message);
    }

    public async Task<(IReadOnlyList<LiveInformation>? Follow, IReadOnlyList<LiveInformation>? Recommend, int NextPageNumber)> GetFeedAsync(int pageNumber, CancellationToken cancellationToken)
    {
        var localToken = _tokenResolver.GetToken();
        var isLogin = localToken is not null;
        var parameters = new Dictionary<string, string>
        {
            { "page", pageNumber.ToString() },
            { "relation_page", pageNumber.ToString() },
            { "scale", "2" },
            { "login_event", isLogin ? "2" : "1" },
            { "device", "phone" },
        };

        var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Get, new Uri(BiliApis.Live.LiveFeed));
        _authenticator.AuthorizeRestRequest(request, parameters);
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseLiveFeedResponse).ConfigureAwait(false);
        var followList = responseObj.Data.CardList
            .Where(p => p.CardType.Contains("idol"))
            .SelectMany(p => p.CardData?.FollowList?.List)
            .Select(p => p.ToLiveInformation()).ToList().AsReadOnly();
        var recommendList = responseObj.Data.CardList
            .Where(p => p.CardType.Contains("small_card") && p.CardData?.LiveCard != null)
            .Select(p => p.CardData.LiveCard.ToLiveInformation()).ToList().AsReadOnly();

        return recommendList.Count == 0
            ? throw new KernelException("直播信息流没有返回数据")
            : ((IReadOnlyList<LiveInformation>? Follow, IReadOnlyList<LiveInformation>? Recommend, int NextPageNumber))(followList, recommendList, pageNumber);
    }

    private static (IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag>? Tags, int NextPageNumber) MapSecondList(BiliDataResponse<LiveSecondListResponse> response, LiveTag? tag, int pageNumber)
    {
        var data = response.Data ?? throw new KernelException("直播分区列表数据缺失");
        var lives = (data.List ?? []).Select(p => p.ToLiveInformation()).ToList().AsReadOnly();
        // 标签只在无标签的首次请求（外部页码 0）返回.
        var tags = pageNumber == 1 && tag is null
            ? (data.Tags ?? []).Select(p => p.ToLiveTag()).ToList().AsReadOnly()
            : null;
        return (lives, tags, pageNumber);
    }

    private static KernelException CreateFailure(int code, string? message)
        => new($"获取直播分区列表失败：{code} ({message ?? "N/A"})");

    private async Task<BiliDataResponse<LiveSecondListResponse>> RequestAppAreaRoomsAsync(Partition partition, LiveTag? tag, int pageNumber, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "page", pageNumber.ToString() },
            { "page_size", "40" },
            { "area_id", partition.Id },
            { "parent_area_id", partition.ParentId ?? string.Empty },
            { "tag_version", "1" },
            { "scale", "2" },
        };

        if (tag != null)
        {
            parameters.Add("sort_type", tag.SortType);
        }

        using var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Get, new Uri(BiliApis.Live.AppAreaRooms));
        _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings { RequireToken = false, ApiType = BiliApiType.App });
        var json = await _httpClient.GetStringAsync(request, cancellationToken, ensureSuccess: true).ConfigureAwait(false);
        return Deserialize(json);
    }

    private async Task<BiliDataResponse<LiveSecondListResponse>> RequestWebAreaRoomsAsync(Partition partition, LiveTag? tag, int pageNumber, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "page", pageNumber.ToString() },
            { "page_size", "40" },
            { "area_id", partition.Id },
            { "parent_area_id", partition.ParentId ?? string.Empty },
            { "sort_type", tag?.SortType ?? "online" },
        };

        await _authenticator.InitializeWbiAsync(cancellationToken).ConfigureAwait(false);
        using var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Get, new Uri(BiliApis.Live.WebAreaRooms));
        _authenticator.AuthorizeRestRequest(request, parameters, BiliAuthorizeExecutionSettings.CookieOnly(needRID: true));
        var json = await _httpClient.GetStringAsync(request, cancellationToken, ensureSuccess: true).ConfigureAwait(false);
        return Deserialize(json);
    }

    private static BiliDataResponse<LiveSecondListResponse> Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, SourceGenerationContext.Default.BiliDataResponseLiveSecondListResponse)
                ?? throw new KernelException("无法解析直播分区列表");
        }
        catch (JsonException ex)
        {
            throw new KernelException("无法解析直播分区列表", ex);
        }
    }

    private bool HasLoginCookie()
        => _cookiesResolver?.GetCookies() is { } cookies
            && cookies.TryGetValue("SESSDATA", out var session)
            && !string.IsNullOrEmpty(session);
}
