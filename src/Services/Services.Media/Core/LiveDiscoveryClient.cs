// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using RichasyKernel;

namespace Richasy.BiliKernel.Services.Media.Core;

internal sealed class LiveDiscoveryClient
{
    private readonly BiliHttpClient _httpClient;
    private readonly BiliAuthenticator _authenticator;
    private readonly IBiliTokenResolver _tokenResolver;

    public LiveDiscoveryClient(
        BiliHttpClient httpClient,
        BiliAuthenticator authenticator,
        IBiliTokenResolver tokenResolver)
    {
        _httpClient = httpClient;
        _authenticator = authenticator;
        _tokenResolver = tokenResolver;
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
        // 服务端不传或用空 sort_type 时会返回空列表（#63），无标签与空标签一律回落到 online.
        var sortType = string.IsNullOrEmpty(tag?.SortType) ? "online" : tag.SortType;
        var (lives, tags) = await RequestAreaDetailAsync(partition, sortType, pageNumber, cancellationToken).ConfigureAwait(false);
        if (lives.Count == 0 && sortType != "online")
        {
            // 个别标签的 sort_type 服务端没有对应数据（如「热门」的 sort_type_2221 恒为 0 条），
            // 回退一次 online 再试；仍为空则按空列表返回，交给界面显示空状态.
            (lives, tags) = await RequestAreaDetailAsync(partition, "online", pageNumber, cancellationToken).ConfigureAwait(false);
        }

        return (lives, tags, pageNumber);
    }

    private async Task<(IReadOnlyList<LiveInformation> Lives, IReadOnlyList<LiveTag> Tags)> RequestAreaDetailAsync(Partition partition, string sortType, int pageNumber, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "page", pageNumber.ToString() },
            { "page_size", "40" },
            { "area_id", partition.Id },
            { "parent_area_id", partition.ParentId },
            { "device", "phone" },
            { "sort_type", sortType },
        };

        var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Get, new Uri(BiliApis.Live.AreaDetail));
        _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings { RequireToken = false, ApiType = BiliApiType.App });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseLiveAreaDetailResponse).ConfigureAwait(false);
        // 服务端偶发返回 "list": null / "new_tags": null，按空集合处理而不是 NRE（#68）.
        var lives = (responseObj.Data.List ?? []).Select(p => p.ToLiveInformation()).ToList().AsReadOnly();
        var tags = (responseObj.Data.Tags ?? []).Select(p => p.ToLiveTag()).ToList().AsReadOnly();
        return (lives, tags);
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
}
