// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;

namespace Richasy.BiliKernel.Bili.Media;

/// <summary>
/// 负责管理与展示视频相关的各种信息。
/// 包括视频推荐、视频排行榜以及视频分区等功能。
/// </summary>
public interface IVideoDiscoveryService
{
    /// <summary>
    /// 获取视频分区.
    /// </summary>
    Task<IReadOnlyList<Partition>> GetVideoPartitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取分区排行榜视频.（仅限主分区，子分区请求会失败）.
    /// </summary>
    Task<IReadOnlyList<VideoInformation>> GetPartitionRankingListAsync(Partition partition, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取分区推荐视频.（仅限主分区，子分区请求会失败）.
    /// </summary>
    Task<(IReadOnlyList<VideoInformation> Videos, long Offset)> GetPartitionRecommendVideoListAsync(Partition partition, long offset = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取子分区视频列表.（仅限主分区下的子分区）.
    /// </summary>
    Task<(IReadOnlyList<VideoInformation> Videos, long Offset, int NextPageNumber)> GetChildPartitionVideoListAsync(Partition childPartition, long offset = 0, int pageNumber = 0, PartitionVideoSortType sortType = PartitionVideoSortType.Default, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取来自桌面客户端的精选视频播放列表.
    /// </summary>
    Task<IReadOnlyList<VideoInformation>> GetCuratedPlaylistAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取推荐视频列表.
    /// 服务端会过滤掉非视频卡片，下发的卡片数不等于最终视频条数.
    /// </summary>
    /// <remarks>
    /// 推荐流不认 `ps`：显式传入页大小不会让服务端多返回内容.
    /// 真正影响单次下发量的是 `column`（版面列数），它决定服务端按几列排版、下发多少张卡片.
    /// </remarks>
    /// <param name="offset">翻页偏移量，首次请求传 0.</param>
    /// <param name="column">请求声明的版面列数（请求参数 column）；小于等于 0 时使用客户端默认值.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetRecommendVideoListAsync(long offset = 0, int column = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取热门视频列表.
    /// </summary>
    Task<(IReadOnlyList<VideoInformation> Items, long Offset)> GetHotVideoListAsync(long offset = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取全站排行榜.
    /// </summary>
    Task<IReadOnlyList<VideoInformation>> GetGlobalRankingListAsync(CancellationToken cancellationToken = default);
}
