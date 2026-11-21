// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Models;

namespace Richasy.BiliKernel.Authenticator;

internal static class BiliRestRequestPolicy
{
    private static readonly Uri BilibiliWeb = new("https://www.bilibili.com/");
    private static readonly Uri MessageCenter = new("https://message.bilibili.com/");

    // Match the method and full endpoint so an unrelated host with the same path inherits no policy.
    internal static (BiliAuthorizeExecutionSettings Settings, Dictionary<string, string>? Parameters) Prepare(
        HttpRequestMessage request, Dictionary<string, string>? parameters, BiliAuthorizeExecutionSettings? settings, bool isAndroidHdToken)
    {
        var endpoint = request.Method == HttpMethod.Get ? request.RequestUri?.GetLeftPart(UriPartial.Path) : null;
        switch (endpoint)
        {
            case BiliApis.Partition.PartitionIndex:
            case BiliApis.Live.LiveArea or BiliApis.Live.AreaDetail:
            case BiliApis.Article.Categories or BiliApis.Article.HotCategories or BiliApis.Article.HotArticles:
                return (new() { ForceNoToken = true }, WithoutAppToken(parameters));
            case BiliApis.Partition.SubPartitionRecommend or BiliApis.Partition.SubPartitionRecommendOffset
                or BiliApis.Partition.SubPartitionNormal or BiliApis.Partition.SubPartitionNormalOffset when isAndroidHdToken:
                return (new() { ForceNoToken = true }, WithoutAppToken(parameters));
            case BiliApis.Account.MessageUnread:
                return PrepareMessageFeed(request, parameters, unread: true);
            case BiliApis.Account.MessageLike or BiliApis.Account.MessageAt or BiliApis.Account.MessageReply:
                return PrepareMessageFeed(request, parameters, unread: false);
            case BiliApis.Account.VideoFavoriteGallery or BiliApis.Account.FavoriteList or BiliApis.Account.VideoFavoriteDelta
                or BiliApis.Account.CollectList or BiliApis.Account.PgcFollowList or BiliApis.Pgc.SeasonUserStatus
                or BiliApis.Account.MyFollowingSearch:
                request.Headers.Referrer ??= BilibiliWeb;
                return (new(BiliApiType.None, forceNoToken: true, useCookie: true) { SkipBuild = true, SkipSign = true }, WithoutAppToken(parameters));
            case BiliApis.Video.PlayerWbi or BiliApis.Account.ViewLaterList:
                return (BiliAuthorizeExecutionSettings.CookieOnly(needRID: true), WithoutAppToken(parameters));
            default:
                return (settings ?? new(), parameters);
        }
    }

    private static (BiliAuthorizeExecutionSettings, Dictionary<string, string>) PrepareMessageFeed(
        HttpRequestMessage request, Dictionary<string, string>? parameters, bool unread)
    {
        var query = parameters is null ? new Dictionary<string, string>() : new(parameters);
        query.Remove("access_key");
        query.Remove("access_token");
        query["build"] = "0";
        query["mobi_app"] = "web";
        query["web_location"] = unread ? "333.1365" : "333.40164";
        if (!unread)
        {
            query["platform"] = "web";
        }

        request.Headers.Referrer ??= MessageCenter;
        return (new(BiliApiType.None, forceNoToken: true, useCookie: true) { SkipSign = true }, query);
    }

    private static Dictionary<string, string>? WithoutAppToken(Dictionary<string, string>? parameters)
    {
        if (parameters is null || (!parameters.ContainsKey("access_key") && !parameters.ContainsKey("access_token")))
        {
            return parameters;
        }

        var query = new Dictionary<string, string>(parameters);
        query.Remove("access_key");
        query.Remove("access_token");
        return query;
    }
}
