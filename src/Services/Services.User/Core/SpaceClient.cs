// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Adapters;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Services.User.Core.Models;
using RichasyKernel;
using System.Globalization;

namespace Richasy.BiliKernel.Services.User.Core;

/// <summary>
/// 用户空间客户端，全部经由 WBI 的 Web 端点.
/// </summary>
internal sealed class SpaceClient
{
    private const string PageSize = "30";

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
        var parameters = new Dictionary<string, string>
        {
            { "mid", userId },
            { "web_location", "1550101" },
        };

        var request = CreateWbiRequest(BiliApis.Account.SpaceInfo, parameters);
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
        var parameters = new Dictionary<string, string>
        {
            { "mid", userId },
            { "ps", PageSize },
            { "pn", (pageNumber + 1).ToString(CultureInfo.InvariantCulture) },
            { "order", "pubdate" },
            { "web_location", "1550101" },
        };

        var request = CreateWbiRequest(BiliApis.Account.SpaceVideos, parameters);
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
        var parameters = new Dictionary<string, string>
        {
            { "mid", userId },
            { "ps", PageSize },
            { "pn", (pageNumber + 1).ToString(CultureInfo.InvariantCulture) },
            { "web_location", "1550101" },
        };

        var request = CreateWbiRequest(BiliApis.Account.SpaceArticles, parameters);
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseSpaceArticleResponse).ConfigureAwait(false);
        var data = responseObj?.Data ?? throw new KernelException("解析用户专栏列表失败");
        return (data.Articles ?? [])
            .Select(CreateArticleInformation)
            .ToList()
            .AsReadOnly();
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

    private HttpRequestMessage CreateWbiRequest(string url, Dictionary<string, string> parameters)
    {
        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(url));
        _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings
        {
            ApiType = BiliApiType.Web,
            NeedRID = true,
            RequireCookie = true,
        });
        return request;
    }
}