// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.BiliKernel.Models.Media;

namespace Richasy.BiliKernel.Bili.Media;

/// <summary>
/// 电影/电视剧/纪录片服务.
/// </summary>
public interface IEntertainmentDiscoveryService
{
    /// <summary>
    /// 获取番剧发布时间线.
    /// </summary>
    Task<(string Title, string Description, IReadOnlyList<TimelineInformation>)> GetBangumiTimelineAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取国创（国内动漫）发布时间线.
    /// </summary>
    Task<(string Title, string Description, IReadOnlyList<TimelineInformation>)> GetDomesticTimelineAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取影视库索引筛选条件.
    /// </summary>
    Task<IReadOnlyList<Filter>> GetIndexFiltersAsync(EntertainmentType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据筛选条件，获取影视作品索引.
    /// </summary>
    Task<(IReadOnlyList<SeasonInformation> Seasons, bool HasNext)> GetIndexSeasonsWithFiltersAsync(EntertainmentType type, Dictionary<Filter, Condition>? filters = default, int pageNumber = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取影视作品的短评或长评.
    /// </summary>
    /// <param name="mediaId">媒体 Id（网页地址里的 md 号）.</param>
    /// <param name="isShort">是否短评.</param>
    /// <param name="pageNumber">页码，从 1 开始.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    Task<(IReadOnlyList<PgcReviewInformation> Reviews, int Total)> GetPgcReviewsAsync(string mediaId, bool isShort, int pageNumber = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// 追番/追剧.
    /// </summary>
    Task FollowAsync(string seasonId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消追番/追剧.
    /// </summary>
    Task UnfollowAsync(string seasonId, CancellationToken cancellationToken = default);
}
