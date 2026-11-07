// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models.Media;

namespace Richasy.BiliKernel.Bili.Media;

/// <summary>
/// 直播弹幕服务：建立并维护直播间弹幕长连接.
/// </summary>
public interface ILiveDanmakuService
{
    /// <summary>
    /// 连接直播间弹幕流；完成认证后才返回。
    /// </summary>
    /// <param name="roomId">直播间标识，支持短号.</param>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>已认证的弹幕会话.</returns>
    /// <exception href="LiveDanmakuUnauthorizedException">认证被服务端拒绝，不应重试.</exception>
    Task<ILiveDanmakuSession> ConnectAsync(string roomId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 一次直播弹幕会话.
/// </summary>
public interface ILiveDanmakuSession : IAsyncDisposable
{
    /// <summary>
    /// 真实直播间标识.
    /// </summary>
    string RoomId { get; }

    /// <summary>
    /// 计数快照.
    /// </summary>
    LiveDanmakuStatistics Statistics { get; }

    /// <summary>
    /// 后台收发循环完成时结束.
    /// </summary>
    Task Completion { get; }

    /// <summary>
    /// 枚举强类型事件；连接中断时内部按策略重连，取消后结束.
    /// </summary>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>事件流.</returns>
    IAsyncEnumerable<LiveDanmakuEvent> GetEventsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 有界停止会话.
    /// </summary>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>停止任务.</returns>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
