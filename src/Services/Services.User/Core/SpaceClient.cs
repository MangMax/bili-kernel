// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Adapters;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Services.User.Core.Models;
using RichasyKernel;
using System.Globalization;
using System.Text;

namespace Richasy.BiliKernel.Services.User.Core;

/// <summary>
/// 用户空间客户端，全部经由 WBI 的 Web 端点.
/// </summary>
internal sealed class SpaceClient
{
    private const string PageSize = "30";

    // 空间 Web 端点要求的浏览器指纹参数，缺失时服务端返回 -352.
    private static readonly Dictionary<string, string> _antiCrawlParameters = new(StringComparer.Ordinal)
    {
        { "dm_img_list", "[]" },
        { "dm_img_str", Convert.ToBase64String(Encoding.UTF8.GetBytes("WebGL 1.0 (OpenGL ES 2.0 Chromium)")) },
        { "dm_cover_img_str", Convert.ToBase64String(Encoding.UTF8.GetBytes("ANGLE (Intel, Intel(R) UHD Graphics 630 Direct3D11 vs_5_0 ps_5_0)")) },
        { "dm_img_inter", "{\"ds\":[],\"wh\":[0,0,0],\"of\":[0,0,0]}" },
    };

    private readonly BiliHttpClient _httpClient;
    private readonly BiliAuthenticator _authenticator;

    public SpaceClient(BiliHttpClient httpClient, BiliAuthenticator authenticator)
    {
        _httpClient = httpClient;
        _authenticator = authenticator;
    }

    public async Task<UserCard> GetUserSpaceInformationAsync(string userId, CancellationToken cancellationToken)
    {
        await _authenticator.InitializeWbiAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new Dictionary<string, string>(_antiCrawlParameters, StringComparer.Ordinal)
        {
            { "mid", userId },
            { "web_location", "1550101" },
        };

        var request = CreateWbiRequest(BiliApis.Account.SpaceInfo, parameters, $"https://space.bilibili.com/{userId}");
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseSpaceInfoResponse).ConfigureAwait(false);
        var data = responseObj?.Data ?? throw new KernelException("解析用户空间信息失败");
        var avatar = string.IsNullOrEmpty(data.Face) ? null : data.Face.ToImage();
        var profile = new UserProfile(data.UserId.ToString(), data.Name, avatar);
        var detail = new UserDetailProfile(profile, data.Sign, data.Level, data.Vip?.Status == 1);
        return new UserCard(detail, null);
    }

    public async Task<(IReadOnlyList<VideoInformation> Videos, int TotalCount)> GetUserVideosAsync(string userId, int pageNumber, CancellationToken cancellationToken)
    {
        await _authenticator.InitializeWbiAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new Dictionary<string, string>(_antiCrawlParameters, StringComparer.Ordinal)
        {
            { "mid", userId },
            { "ps", PageSize },
            { "pn", (pageNumber + 1).ToString(CultureInfo.InvariantCulture) },
            { "order", "pubdate" },
            { "web_location", "1550101" },
        };

        var request = CreateWbiRequest(BiliApis.Account.SpaceVideos, parameters, $"https://space.bilibili.com/{userId}/video");
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseSpaceVideoResponse).ConfigureAwait(false);
        var data = responseObj?.Data ?? throw new KernelException("解析用户投稿列表失败");
        var videos = data.List?.Videos ?? [];
        var total = data.Page?.Count ?? videos.Count;
        var result = videos
            .Select(p => CreateVideoInformation(p, userId))
            .ToList()
            .AsReadOnly();
        return (result, total);
    }

    public async Task<IReadOnlyList<ArticleInformation>> GetUserArticlesAsync(string userId, int pageNumber, CancellationToken cancellationToken)
    {
        await _authenticator.InitializeWbiAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new Dictionary<string, string>(_antiCrawlParameters, StringComparer.Ordinal)
        {
            { "mid", userId },
            { "ps", PageSize },
            { "pn", (pageNumber + 1).ToString(CultureInfo.InvariantCulture) },
            { "web_location", "1550101" },
        };

        var request = CreateWbiRequest(BiliApis.Account.SpaceArticles, parameters, $"https://space.bilibili.com/{userId}/article");
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseSpaceArticleResponse).ConfigureAwait(false);
        var data = responseObj?.Data ?? throw new KernelException("解析用户专栏列表失败");
        return (data.Articles ?? [])
            .Select(CreateArticleInformation)
            .ToList()
            .AsReadOnly();
    }

    public async Task<(IReadOnlyList<AudioInformation> Audios, int TotalCount)> GetUserAudiosAsync(string userId, int pageNumber, CancellationToken cancellationToken)
    {
        await _authenticator.InitializeWbiAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new Dictionary<string, string>(_antiCrawlParameters, StringComparer.Ordinal)
        {
            { "uid", userId },
            { "ps", PageSize },
            { "pn", (pageNumber + 1).ToString(CultureInfo.InvariantCulture) },
            { "web_location", "1550101" },
        };

        var request = CreateWbiRequest(BiliApis.Account.SpaceAudios, parameters, $"https://space.bilibili.com/{userId}/audio");
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseSpaceAudioResponse).ConfigureAwait(false);
        if (responseObj is null)
        {
            throw new KernelException("解析用户音频列表失败");
        }

        if (!responseObj.IsSuccess())
        {
            throw new KernelException($"获取用户音频列表失败：{responseObj.Code} {responseObj.Message}");
        }

        // 未登录或用户没有音频时服务端返回 data: null，按空列表处理.
        var data = responseObj.Data;
        var audios = (data?.Items ?? [])
            .Select(item =>
            {
                var cover = string.IsNullOrEmpty(item.Cover) ? null : item.Cover.ToImage();
                return new AudioInformation(new MediaIdentifier(item.Id.ToString(CultureInfo.InvariantCulture), item.Title, cover), item.Author, item.Duration, item.PlayCount);
            })
            .ToList()
            .AsReadOnly();
        var total = data is { TotalSize: > 0 } ? data.TotalSize : audios.Count;
        return (audios, total);
    }

    public async Task<(IReadOnlyList<UserCollectionInfo> Collections, int TotalCount)> GetUserCollectionInfosAsync(string userId, int pageNumber, CancellationToken cancellationToken)
    {
        // 合集端点不接受 WBI 签名与空间反爬参数（会返回 -400），公开数据也不需要登录，
        // 因此只带分页参数并补上来源页头.
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "mid", userId },
            { "page_num", (pageNumber + 1).ToString(CultureInfo.InvariantCulture) },
            // 该端点最多接受 20 条/页，超过会返回 -400.
            { "page_size", "20" },
            { "web_location", "333.1387" },
        };

        var query = string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri($"{BiliApis.Account.SpaceCollections}?{query}"));
        request.Headers.TryAddWithoutValidation("Referer", $"https://space.bilibili.com/{userId}/channel/collection");
        request.Headers.TryAddWithoutValidation("Origin", "https://space.bilibili.com");
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseSpaceCollectionResponse).ConfigureAwait(false);
        if (responseObj is null)
        {
            throw new KernelException("解析用户合集列表失败");
        }

        if (!responseObj.IsSuccess())
        {
            throw new KernelException($"获取用户合集列表失败：{responseObj.Code} {responseObj.Message}");
        }

        var data = responseObj.Data;
        var collections = new List<UserCollectionInfo>();
        foreach (var entry in data?.ItemsLists?.Seasons ?? [])
        {
            if (entry.Meta is { SeasonId: > 0 } meta)
            {
                collections.Add(CreateCollection(meta.SeasonId!.Value.ToString(CultureInfo.InvariantCulture), meta, true));
            }
        }

        foreach (var entry in data?.ItemsLists?.Series ?? [])
        {
            if (entry.Meta is { SeriesId: > 0 } meta)
            {
                collections.Add(CreateCollection(meta.SeriesId!.Value.ToString(CultureInfo.InvariantCulture), meta, false));
            }
        }

        var total = data?.Page?.Total ?? collections.Count;
        return (collections.AsReadOnly(), total);
    }

    private static UserCollectionInfo CreateCollection(string id, SpaceCollectionMeta meta, bool isSeason)
    {
        var cover = string.IsNullOrEmpty(meta.Cover) ? null : meta.Cover.ToImage();
        return new UserCollectionInfo(id, meta.Name, cover, meta.Total, meta.Description, isSeason);
    }

    private static VideoInformation CreateVideoInformation(SpaceVideoItem item, string userId)
    {
        var cover = string.IsNullOrEmpty(item.Cover) ? null : item.Cover.ToImage();
        var identifier = new MediaIdentifier(item.Aid.ToString(), item.Title, cover);
        var publisher = new PublisherProfile(new UserProfile(userId));
        var community = new VideoCommunityInformation(item.Aid.ToString(), item.PlayCount, item.DanmakuCount);
        var publishTime = item.Created.HasValue
            ? DateTimeOffset.FromUnixTimeSeconds(item.Created.Value).ToLocalTime()
            : (DateTimeOffset?)null;
        return new VideoInformation(identifier, publisher, (item.Length ?? "0").ToDurationSeconds(), item.BvId, publishTime, communityInformation: community);
    }

    private static ArticleInformation CreateArticleInformation(SpaceArticleItem item)
    {
        var cover = item.Images is { Count: > 0 } && !string.IsNullOrEmpty(item.Images[0])
            ? item.Images[0].ToImage()
            : null;
        var identifier = new ArticleIdentifier(item.Id.ToString(), item.Title, item.Summary, cover);
        var publishTime = item.PublishTime > 0
            ? DateTimeOffset.FromUnixTimeSeconds(item.PublishTime).ToLocalTime()
            : (DateTimeOffset?)null;
        return new ArticleInformation(identifier, publishDateTime: publishTime);
    }

    private HttpRequestMessage CreateWbiRequest(string url, Dictionary<string, string> parameters, string referer)
    {
        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(url));

        // 空间端点会校验来源页，缺失 Referer 时 Web 端返回 -352、WAF 直接返回 412.
        request.Headers.TryAddWithoutValidation("Referer", referer);
        request.Headers.TryAddWithoutValidation("Origin", "https://space.bilibili.com");
        _authenticator.AuthorizeRestRequest(request, parameters, BiliAuthorizeExecutionSettings.CookieOnly(needRID: true));
        return request;
    }
}