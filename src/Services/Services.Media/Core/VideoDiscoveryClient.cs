// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Bilibili.App.Show.Popular.V1;
using Bilibili.App.Show.Rank.V1;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using RichasyKernel;

namespace Richasy.BiliKernel.Services.Media.Core;

internal sealed class VideoDiscoveryClient
{
    /// <summary>
    /// 网页端推荐流的默认页大小.
    /// </summary>
    private const int DefaultCuratedPageSize = 10;

    private readonly BiliHttpClient _httpClient;
    private readonly BiliAuthenticator _authenticator;
    private readonly IBiliTokenResolver _tokenResolver;

    public VideoDiscoveryClient(
        BiliHttpClient httpClient,
        BiliAuthenticator authenticator,
        IBiliTokenResolver tokenResolver)
    {
        _httpClient = httpClient;
        _authenticator = authenticator;
        _tokenResolver = tokenResolver;
    }

    public async Task<IReadOnlyList<Partition>> GetVideoPartitionsAsync(CancellationToken cancellationToken)
    {
        var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Get, new Uri(BiliApis.Partition.PartitionIndex));
        _authenticator.AuthorizeRestRequest(request, settings: new BiliAuthorizeExecutionSettings { RequireToken = false });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseListVideoPartition).ConfigureAwait(false);
        var items = responseObj.Data.Where(p => p.IsNeedToShow()).Select(p => p.ToPartition()).ToList();
        return items.Count == 0
            ? throw new KernelException("视频分区数据为空")
            : (IReadOnlyList<Partition>)items.AsReadOnly();
    }

    public async Task<IReadOnlyList<VideoInformation>> GetPartitionRankingListAsync(Partition partition, CancellationToken cancellationToken)
    {
        var req = new RankRegionResultReq
        {
            Rid = Convert.ToInt32(partition.Id),
        };
        var request = BiliHttpClient.CreateRequest(new Uri(BiliApis.Home.RankingGRPC), req);
        _authenticator.AuthorizeGrpcRequest(request, false);
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, RankListReply.Parser).ConfigureAwait(false);
        return responseObj.Items is null || responseObj.Items.Count == 0
            ? throw new KernelException("无法获取到有效的视频列表，请稍后再试")
            : (IReadOnlyList<VideoInformation>)responseObj.Items.Select(p => p.ToVideoInformation()).ToList().AsReadOnly();
    }

    public async Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> GetPartitionRecommendVideoListAsync(Partition partition, long offset, CancellationToken cancellationToken)
    {
        var isOffset = offset > 0;
        var url = isOffset ? BiliApis.Partition.SubPartitionRecommendOffset : BiliApis.Partition.SubPartitionRecommend;
        var parameters = new Dictionary<string, string>
        {
            { "rid", partition.Id },
            { "pull", "0" },
        };

        if (isOffset)
        {
            parameters.Add("ctime", offset.ToString());
        }

        var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Get, new Uri(url));
        _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings { RequireToken = false });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var data = (await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseSubPartition).ConfigureAwait(false)).Data;
        var videos = data.NewVideos
            .Concat(data.RecommendVideos ?? new List<PartitionVideo>())
            .Distinct()
            .Select(p => p.ToVideoInformation());

        var offsetId = data.BottomOffsetId;
        return !videos.Any()
            ? throw new KernelException("无法获取到有效的视频列表，请稍后再试")
            : ((IReadOnlyList<VideoInformation> Videos, long Offset))(videos.ToList().AsReadOnly(), offsetId);
    }

    public async Task<(IReadOnlyList<VideoInformation> Videos, long Offset, int NextPageNumber)> GetChildPartitionVideoListAsync(Partition partition, long offset, int pageNumber, PartitionVideoSortType sort, CancellationToken cancellationToken)
    {
        var isDefaultOrder = sort == PartitionVideoSortType.Default;
        var isOffset = offset > 0;
        var url = isDefaultOrder
                ? isOffset ? BiliApis.Partition.SubPartitionNormalOffset : BiliApis.Partition.SubPartitionNormal
                : BiliApis.Partition.SubPartitionOrderOffset;
        var parameters = new Dictionary<string, string>
        {
            { "rid", partition.Id },
            { "pull", "0" },
        };

        if (isOffset)
        {
            parameters.Add("ctime", offset.ToString());
        }

        if (!isDefaultOrder)
        {
            var sortStr = sort switch
            {
                PartitionVideoSortType.Newest => "senddate",
                PartitionVideoSortType.Play => "view",
                PartitionVideoSortType.Danmaku => "danmaku",
                PartitionVideoSortType.Favorite => "favorite",
                PartitionVideoSortType.Reply => "reply",
                _ => default,
            };

            parameters.Add("order", sortStr);
            parameters.Add("pn", pageNumber.ToString());
            parameters.Add("ps", "30");
        }

        SubPartition data;
        if (!isDefaultOrder)
        {
            // dynamic/region 已下线（返回 -404），排序列表改由 newlist 提供.
            var videoList = await RequestPartitionArchivesCoreAsync(BiliApis.Partition.WebRegionNewList, parameters, cancellationToken).ConfigureAwait(false);
            data = new SubPartition
            {
                NewVideos = videoList,
            };
        }
        else
        {
            var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Get, new Uri(url));
            _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings { RequireToken = false });
            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            data = (await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseSubPartition).ConfigureAwait(false)).Data;
        }

        var videos = data.NewVideos
            .Concat(data.RecommendVideos ?? new List<PartitionVideo>())
            .Distinct()
            .Select(p => p.ToVideoInformation());

        var offsetId = data.BottomOffsetId;
        var nextPageNumber = !isDefaultOrder ? pageNumber : 1;

        return !videos.Any()
            ? throw new KernelException("无法获取到有效的视频列表，请稍后再试")
            : ((IReadOnlyList<VideoInformation> Videos, long Offset, int NextPageNumber))(videos.ToList().AsReadOnly(), offsetId, nextPageNumber);
    }

    private async Task<List<PartitionVideo>> RequestPartitionArchivesCoreAsync(string url, Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var requestParameters = new Dictionary<string, string>(parameters, StringComparer.Ordinal);
        var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Get, new Uri(url));
        _authenticator.AuthorizeRestRequest(request, requestParameters, new BiliAuthorizeExecutionSettings { ApiType = BiliApiType.Web, RequireToken = false });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponsePartitionArchiveResponse).ConfigureAwait(false);
        return responseObj?.Data?.Archives ?? [];
    }

    public async Task<IReadOnlyList<VideoInformation>> GetCuratedPlaylistAsync(CancellationToken cancellationToken)
    {
        var (items, _) = await GetCuratedFeedAsync(0, 0, cancellationToken).ConfigureAwait(false);
        return items;
    }

    public async Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetCuratedFeedAsync(long freshIndex, int pageSize, CancellationToken cancellationToken)
    {
        // 网页端推荐按「刷新批次」推进：每次请求给一个新的 fresh_idx，服务端回一批新卡片。
        var parameters = new Dictionary<string, string>
        {
            { "fresh_idx", (freshIndex <= 0 ? 1 : freshIndex).ToString() },
            { "ps", (pageSize > 0 ? pageSize : DefaultCuratedPageSize).ToString() },
            { "plat", "1" },
            { "feed_version", "CLIENT_SELECTED" },
            { "fresh_type", "0" },
            { "web_location", "bilibili-electron" },
        };

        await _authenticator.InitializeWbiAsync(cancellationToken).ConfigureAwait(false);
        var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Get, new Uri(BiliApis.Home.CuratedPlaylist));
        _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings
        {
            ApiType = BiliApiType.Web,
            NeedRID = true,
            RequireCookie = true,
            ForceNoToken = true,
        });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseCuratedPlaylistResponse).ConfigureAwait(false);
        return responseObj.Data is null || responseObj.Data.Items is null || responseObj.Data.Items.Count == 0
            ? throw new KernelException("精选视频数据为空")
            : ((IReadOnlyList<VideoInformation> Items, long Offset))(
                responseObj.Data.Items.Select(p => p.ToVideoInformation()).ToList().AsReadOnly(),
                Math.Max(1, freshIndex) + 1);
    }

    public async Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> GetRecommendVideoListAsync(long offset, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "idx", offset.ToString() },
            { "flush", "5" },
            // 实测该端点只接受 4；5/6/7/8 一律返回 -400「请求错误」。
            // 单页条数因此是服务端决定的，客户端没有可用的「多要一点」参数。
            { "column", "4" },
            { "device", "desktop" },
            { "pull", (offset == 0).ToString().ToLowerInvariant() },
        };

        var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Get, new Uri(BiliApis.Home.Recommend));
        _authenticator.AuthorizeRestRequest(request, parameters);
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObject = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseRecommendVideoResponse).ConfigureAwait(false);
        var nextOffset = responseObject.Data.Items.Last().Idx;
        var videos = responseObject.Data.Items.Where(p => p.CardGoto is "av")
            .Select(p => p.ToVideoInformation()).ToList().AsReadOnly();
        return videos.Count == 0
            ? throw new KernelException("推荐视频数据为空")
            : ((IReadOnlyList<VideoInformation> Videos, long Offset))(videos, nextOffset);
    }

    public async Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> GetHotVideoListAsync(long offset, CancellationToken cancellationToken)
    {
        var localToken = _tokenResolver.GetToken();
        var isLogin = localToken is not null;
        var req = new PopularResultReq()
        {
            Idx = offset,
            LoginEvent = isLogin ? 2 : 1,
        };

        var request = BiliHttpClient.CreateRequest(new Uri(BiliApis.Home.PopularGRPC), req);
        _authenticator.AuthorizeGrpcRequest(request, false);
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, PopularReply.Parser).ConfigureAwait(false);
        var videos = responseObj.Items
            .Where(p => p.ItemCase == Bilibili.App.Card.V1.Card.ItemOneofCase.SmallCoverV5
                && p.SmallCoverV5 != null
                && p.SmallCoverV5.Base.CardGoto == "av"
                && !p.SmallCoverV5.Base.Uri.Contains("bangumi"));
        var nextOffset = videos.Last().SmallCoverV5.Base.Idx;
        return !videos.Any()
            ? throw new KernelException("热门视频数据为空")
            : ((IReadOnlyList<VideoInformation> Videos, long Offset))(videos.Select(p => p.ToVideoInformation()).ToList().AsReadOnly(), nextOffset);
    }

    public async Task<IReadOnlyList<VideoInformation>> GetGlobalRankingListAsync(CancellationToken cancellationToken)
    {
        var req = new RankRegionResultReq
        {
            Rid = 0,
        };
        var request = BiliHttpClient.CreateRequest(new Uri(BiliApis.Home.RankingGRPC), req);
        _authenticator.AuthorizeGrpcRequest(request, false);
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, RankListReply.Parser).ConfigureAwait(false);
        return responseObj.Items is null || responseObj.Items.Count == 0
            ? throw new KernelException("全站排行榜数据为空")
            : (IReadOnlyList<VideoInformation>)responseObj.Items.Select(p => p.ToVideoInformation()).ToList().AsReadOnly();
    }
}
