// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Models.Media;

/// <summary>
/// 直播弹幕连接计数快照.
/// </summary>
/// <remarks>
/// 恒等式：<c>DecodedPackets == ParsedMessages + UnknownCommands + MalformedMessages + IgnoredMessages + SkippedPackets</c>.
/// </remarks>
public sealed class LiveDanmakuStatistics
{
    /// <summary>
    /// 收到的 WebSocket 消息数.
    /// </summary>
    public long ReceivedFrames { get; init; }

    /// <summary>
    /// 解码出的协议包数（含压缩帧与内层包）.
    /// </summary>
    public long DecodedPackets { get; init; }

    /// <summary>
    /// 成功解析为强类型事件的消息数.
    /// </summary>
    public long ParsedMessages { get; init; }

    /// <summary>
    /// 未知命令数.
    /// </summary>
    public long UnknownCommands { get; init; }

    /// <summary>
    /// 损坏消息数.
    /// </summary>
    public long MalformedMessages { get; init; }

    /// <summary>
    /// 已知但不产出事件的重复消息数（如日文醒目留言）.
    /// </summary>
    public long IgnoredMessages { get; init; }

    /// <summary>
    /// 无需产出事件的控制包数（心跳、认证回执等）.
    /// </summary>
    public long SkippedPackets { get; init; }

    /// <summary>
    /// 重连次数.
    /// </summary>
    public long Reconnects { get; init; }
}
