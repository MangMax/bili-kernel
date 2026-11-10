// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models.Media;
namespace Richasy.BiliKernel.Bili.Media;

/// <summary>
/// 播放器服务，可以获取播放信息.
/// </summary>
public interface IPlayerService
{
    /// <summary>获取公开的视频元数据，不读取播放进度或用户互动状态。</summary>
    Task<VideoInformation> GetVideoInformationAsync(MediaIdentifier video, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取视频页面详情.
    /// </summary>
    Task<VideoPlayerView> GetVideoPageDetailAsync(MediaIdentifier video, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取视频播放信息.
    /// </summary>
    Task<DashMediaInformation> GetVideoPlayDetailAsync(MediaIdentifier video, long cid, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取视频章节列表.
    /// </summary>
    /// <param name="video">视频标识符.</param>
    /// <param name="cid">视频分 P 的 cid.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>章节列表，UP 主未标记章节时返回空集合.</returns>
    /// <remarks>
    /// 对应哔哩哔哩播放器的“章节”功能，数据来自 /x/player/wbi/v2 的 view_points 字段.
    /// </remarks>
    Task<IReadOnlyList<VideoChapter>> GetVideoChaptersAsync(MediaIdentifier video, long cid, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取直播页面详情.
    /// </summary>
    Task<LivePlayerView> GetLivePageDetailAsync(MediaIdentifier live, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取直播播放信息.
    /// </summary>
    Task<LiveMediaInformation> GetLivePlayDetailAsync(MediaIdentifier live, int quality = 400, bool audioOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取直播间最近的弹幕，用于进房补屏。
    /// </summary>
    /// <param name="live">直播间标识，支持短号.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>最近约 20 条弹幕，按"房主/管理"在前、"普通观众"在后排列；服务端无数据时返回空集合.</returns>
    /// <remarks>
    /// 数据来自网页端的历史弹幕接口，只覆盖进房前的最近一小段时间，不代表完整历史；
    /// 直播间的醒目留言没有可靠的历史列表，因此本方法不返回醒目留言.
    /// </remarks>
    Task<IReadOnlyList<LiveDanmakuInformation>> GetRecentLiveDanmakuAsync(MediaIdentifier live, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取直播间可用的表情包，用于把正文里的 [名称] 渲染成图片。
    /// </summary>
    /// <param name="roomId">直播间标识.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>表情列表；未登录或接口失败时返回空集合.</returns>
    Task<IReadOnlyList<LiveEmoticonItem>> GetLiveEmoticonsAsync(string roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取直播头衔列表，用于把弹幕里的 title-233-1 映射成图标。
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>头衔列表；未登录时为空集合.</returns>
    Task<IReadOnlyList<LiveTitleItem>> GetLiveTitlesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取 PGC 页面详情.
    /// </summary>
    Task<PgcPlayerView> GetPgcPageDetailAsync(string? seasonId = default, string? episodeId = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取 PGC 播放信息.
    /// </summary>
    /// <param name="cid">视频分 P 的 cid.</param>
    /// <param name="episodeId">剧集标识.</param>
    /// <param name="seasonType">剧集类型.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>播放信息.</returns>
    /// <remarks>
    /// 仅可试看时返回带 <see cref="DashMediaInformation.IsPreview"/> 与 <see cref="DashMediaInformation.PreviewUrls"/> 的结果，不抛出异常；取消与网络错误照常传播.
    /// </remarks>
    /// <exception cref="PgcPlaybackUnavailableException">番剧播放不可用，原因见 <see cref="PgcPlaybackUnavailableException.Reason"/>.</exception>
    Task<DashMediaInformation> GetPgcPlayDetailAsync(string cid, string episodeId, string seasonType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取番剧分集交互信息.
    /// </summary>
    Task<VideoOperationInformation> GetEpisodeOperationInformationAsync(string episodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取在线观看人数.
    /// </summary>
    Task<OnlineViewer> GetOnlineViewerAsync(string aid, string cid, CancellationToken cancellationToken);

    /// <summary>
    /// 切换视频点赞状态.
    /// </summary>
    Task ToggleVideoLikeAsync(string aid, bool isLike, CancellationToken cancellationToken = default);

    /// <summary>
    /// 投币.
    /// </summary>
    Task CoinVideoAsync(string aid, int number, bool alsoLike, CancellationToken cancellationToken = default);

    /// <summary>
    /// 收藏视频.
    /// </summary>
    Task FavoriteVideoAsync(string aid, IList<string> favoriteIds, IList<string> unfavoriteIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 收藏剧集.
    /// </summary>
    Task FavoriteEpisodeAsync(string aid, IList<string> favoriteIds, IList<string> unfavoriteIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 一键三连.
    /// </summary>
    Task TripleVideoAsync(string aid, CancellationToken cancellationToken = default);

    /// <summary>
    /// 报告视频播放进度.
    /// </summary>
    Task ReportVideoProgressAsync(string aid, string cid, int progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// 报告剧集播放进度.
    /// </summary>
    Task ReportEpisodeProgressAsync(string aid, string cid, string episodeId, string seasonId, int progress, CancellationToken cancellationToken = default);

}
