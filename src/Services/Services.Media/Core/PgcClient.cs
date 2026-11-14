// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media.Core.Models;
using RichasyKernel;
using System.Text.Json;

namespace Richasy.BiliKernel.Services.Media.Core;

/// <summary>
/// 动漫客户端.
/// </summary>
internal sealed class PgcClient
{
    private readonly BiliHttpClient _httpClient;
    private readonly BiliAuthenticator _authenticator;

    public PgcClient(
        BiliHttpClient httpClient,
        BiliAuthenticator authenticator)
    {
        _httpClient = httpClient;
        _authenticator = authenticator;
    }

    public async Task<(string Title, string Description, IReadOnlyList<TimelineInformation>)> GetAnimeTimelineAsync(bool isBangumi, CancellationToken cancellationToken = default)
    {
        PgcTimeLineResponse? content;
        try
        {
            var webResponse = await RequestTimelineAsync(BiliApis.Pgc.TimeLine, isBangumi, true, cancellationToken).ConfigureAwait(false);
            content = webResponse?.Content;
        }
        catch (Exception ex) when (ex is KernelException or JsonException)
        {
            content = null;
        }

        if (content?.Data is null || content.Data.Count == 0)
        {
            // 拿不到数据时回退旧版路径.
            var legacyResponse = await RequestTimelineAsync(BiliApis.Pgc.TimeLineLegacy, isBangumi, false, cancellationToken).ConfigureAwait(false);
            content = legacyResponse?.Content;
        }

        if (content is null)
        {
            throw new KernelException("解析番剧时间线失败");
        }

        var timelineItems = (content.Data ?? []).Select(p => p.ToTimelineInformation()).ToList().AsReadOnly();
        if (timelineItems.Count == 0)
        {
            var name = isBangumi ? "番剧" : "国创";
            throw new KernelException($"{name}时间线数据为空");
        }

        return (content.Title ?? string.Empty, content.Subtitle ?? string.Empty, timelineItems);
    }

    private async Task<PgcTimeLineEnvelope?> RequestTimelineAsync(string url, bool isBangumi, bool isWeb, CancellationToken cancellationToken)
    {
        var parameters = isWeb
            ? new Dictionary<string, string>
            {
                { "types", isBangumi ? "1" : "4" },
                { "before", "3" },
                { "after", "7" },
            }
            : new Dictionary<string, string>
            {
                { "type", isBangumi ? "2" : "3" },
                { "filter_type", "0" },
            };

        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(url));
        _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings { RequireToken = false });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.PgcTimeLineEnvelope).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<Filter>> GetEntertainmentFiltersAsync(EntertainmentType type, CancellationToken cancellationToken)
    {
        var parameters = type == EntertainmentType.Anime ? GetAnimeIndexParameters() : GetEntertainmentIndexParameters(type);
        return GetPgcIndexFiltersAsync(parameters, cancellationToken);
    }

    public Task<(IReadOnlyList<SeasonInformation> Seasons, bool HasNext)> GetEntertainmentIndexWithFiltersAsync(EntertainmentType type, Dictionary<Filter, Condition>? filters, int page, CancellationToken cancellationToken)
    {
        var parameters = type == EntertainmentType.Anime ? GetAnimeIndexParameters() : GetEntertainmentIndexParameters(type);
        return GetPgcSeasonsWithFiltersAsync(parameters, filters, page, cancellationToken);
    }

    public async Task<(IReadOnlyList<PgcReviewInformation> Reviews, int Total)> GetReviewsAsync(string mediaId, bool isShort, int pageNumber, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "media_id", mediaId },
            { "ps", "20" },
            { "sort", "0" },
        };

        if (isShort)
        {
            parameters["cursor"] = ((pageNumber - 1) * 20).ToString();
        }
        else
        {
            parameters["pn"] = pageNumber.ToString();
        }

        var url = isShort ? BiliApis.Pgc.ShortReviewList : BiliApis.Pgc.ReviewList;
        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(url));
        _authenticator.AuthorizeRestRequest(request, parameters);
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.PgcReviewResponse).ConfigureAwait(false);
        var data = responseObj?.Data;
        var reviews = data?.List is { Count: > 0 } list
            ? list.Select(p => p.ToReviewInformation(isShort)).ToList()
            : [];
        var total = data is null ? 0 : Math.Max(data.Count, data.Total);
        return (reviews, total);
    }

    public async Task ToggleFollowAsync(string seasonId, bool isFollow, CancellationToken cancellationToken)
    {
        var paramters = new Dictionary<string, string>
        {
            { "season_id", seasonId }
        };

        var url = isFollow ? BiliApis.Pgc.Follow : BiliApis.Pgc.Unfollow;
        var request = BiliHttpClient.CreateRequest(HttpMethod.Post, new Uri(url));
        _authenticator.AuthorizeRestRequest(request, paramters, BiliAuthorizeExecutionSettings.CookieOnly(needCSRF: true));
        await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<Filter>> GetPgcIndexFiltersAsync(Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(BiliApis.Pgc.IndexCondition));
        _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings { RequireToken = false });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponsePgcIndexConditionResponse).ConfigureAwait(false);
        var filters = responseObj.Data.FilterList.Select(p => p.ToFilter()).ToList();
        if (responseObj.Data?.OrderList?.Count > 0)
        {
            var orderFilter = new Filter("排序方式", "order", responseObj.Data.OrderList.Select(p => new Condition(p.Name, p.Field)).ToList());
            filters.Insert(0, orderFilter);
        }

        return filters.Count == 0 ? throw new KernelException("筛选条件为空") : (IReadOnlyList<Filter>)filters.AsReadOnly();
    }

    private async Task<(IReadOnlyList<SeasonInformation> Seasons, bool HasNext)> GetPgcSeasonsWithFiltersAsync(Dictionary<string, string> parameters, Dictionary<Filter, Condition>? filters, int page, CancellationToken cancellationToken)
    {
        parameters.Add("pagesize", "30");
        parameters.Add("page", page.ToString());
        if (filters != null)
        {
            foreach (var item in filters)
            {
                parameters.Add(item.Key.Id, item.Value.Id);
            }
        }

        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(BiliApis.Pgc.IndexResult));
        _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings { RequireToken = false });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponsePgcIndexResultResponse).ConfigureAwait(false);
        var seasons = responseObj.Data.List.Select(p => p.ToSeasonInformation()).ToList().AsReadOnly();
        return seasons.Count == 0
            ? throw new KernelException("无法获取到有效的剧集列表，请检查参数或稍后重试")
            : ((IReadOnlyList<SeasonInformation> Seasons, bool HasNext))(seasons, responseObj.Data.HasNext != 0);
    }

    private static Dictionary<string, string> GetAnimeIndexParameters()
    {
        return new Dictionary<string, string>
        {
            { "season_type", "1" },
            { "type", "0" }
        };
    }

    private static Dictionary<string, string> GetEntertainmentIndexParameters(EntertainmentType type)
    {
        var indexType = type switch
        {
            EntertainmentType.Movie => "2",
            EntertainmentType.TV => "5",
            EntertainmentType.Documentary => "3",
            _ => throw new NotSupportedException()
        };

        return new Dictionary<string, string>
        {
            { "index_type", indexType },
            { "type", "0" }
        };
    }
}
