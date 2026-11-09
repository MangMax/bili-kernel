// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Models.Media;

/// <summary>
/// 直播弹幕事件基类.
/// </summary>
public abstract class LiveDanmakuEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveDanmakuEvent"/> class.
    /// </summary>
    /// <param name="command">触发的协议命令.</param>
    /// <param name="receivedAt">接收时间.</param>
    protected LiveDanmakuEvent(string command, DateTimeOffset receivedAt)
    {
        Command = command;
        ReceivedAt = receivedAt;
    }

    /// <summary>
    /// 触发的协议命令.
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// 接收时间.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; }
}

/// <summary>
/// 收到一条弹幕.
/// </summary>
public sealed class LiveDanmakuReceivedEvent : LiveDanmakuEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveDanmakuReceivedEvent"/> class.
    /// </summary>
    /// <param name="danmaku">弹幕内容.</param>
    /// <param name="command">触发的协议命令.</param>
    /// <param name="receivedAt">接收时间.</param>
    public LiveDanmakuReceivedEvent(LiveDanmakuInformation danmaku, string command, DateTimeOffset receivedAt)
        : base(command, receivedAt)
        => Danmaku = danmaku;

    /// <summary>
    /// 弹幕内容.
    /// </summary>
    public LiveDanmakuInformation Danmaku { get; }
}

/// <summary>
/// 收到一条醒目留言.
/// </summary>
public sealed class LiveSuperChatEvent : LiveDanmakuEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveSuperChatEvent"/> class.
    /// </summary>
    /// <param name="superChat">醒目留言内容.</param>
    /// <param name="command">触发的协议命令.</param>
    /// <param name="receivedAt">接收时间.</param>
    public LiveSuperChatEvent(LiveSuperChatInformation superChat, string command, DateTimeOffset receivedAt)
        : base(command, receivedAt)
        => SuperChat = superChat;

    /// <summary>
    /// 醒目留言内容.
    /// </summary>
    public LiveSuperChatInformation SuperChat { get; }
}

/// <summary>
/// 醒目留言被删除.
/// </summary>
public sealed class LiveSuperChatDeletedEvent : LiveDanmakuEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveSuperChatDeletedEvent"/> class.
    /// </summary>
    /// <param name="messageIds">被删除的醒目留言标识.</param>
    /// <param name="command">触发的协议命令.</param>
    /// <param name="receivedAt">接收时间.</param>
    public LiveSuperChatDeletedEvent(IReadOnlyList<string> messageIds, string command, DateTimeOffset receivedAt)
        : base(command, receivedAt)
        => MessageIds = messageIds;

    /// <summary>
    /// 被删除的醒目留言标识.
    /// </summary>
    public IReadOnlyList<string> MessageIds { get; }
}

/// <summary>
/// 在线人数变化.
/// </summary>
public sealed class LiveOnlineCountChangedEvent : LiveDanmakuEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveOnlineCountChangedEvent"/> class.
    /// </summary>
    /// <param name="count">人数或人气值.</param>
    /// <param name="source">来源.</param>
    /// <param name="text">服务端展示文本.</param>
    /// <param name="command">触发的协议命令.</param>
    /// <param name="receivedAt">接收时间.</param>
    public LiveOnlineCountChangedEvent(int count, LiveOnlineCountSource source, string? text, string command, DateTimeOffset receivedAt)
        : base(command, receivedAt)
    {
        Count = count;
        Source = source;
        Text = text;
    }

    /// <summary>
    /// 人数或人气值.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// 数据来源.
    /// </summary>
    public LiveOnlineCountSource Source { get; }

    /// <summary>
    /// 服务端展示文本.
    /// </summary>
    public string? Text { get; }
}

/// <summary>
/// 房间标题变更.
/// </summary>
public sealed class LiveRoomTitleChangedEvent : LiveDanmakuEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveRoomTitleChangedEvent"/> class.
    /// </summary>
    /// <param name="title">新标题.</param>
    /// <param name="areaName">分区名称.</param>
    /// <param name="command">触发的协议命令.</param>
    /// <param name="receivedAt">接收时间.</param>
    public LiveRoomTitleChangedEvent(string title, string? areaName, string command, DateTimeOffset receivedAt)
        : base(command, receivedAt)
    {
        Title = title;
        AreaName = areaName;
    }

    /// <summary>
    /// 新标题.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 分区名称.
    /// </summary>
    public string? AreaName { get; }
}

/// <summary>
/// 直播间未开播（主播准备中或已下播）.
/// </summary>
public sealed class LiveRoomNotStartedEvent : LiveDanmakuEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveRoomNotStartedEvent"/> class.
    /// </summary>
    /// <param name="command">触发的协议命令.</param>
    /// <param name="receivedAt">接收时间.</param>
    public LiveRoomNotStartedEvent(string command, DateTimeOffset receivedAt)
        : base(command, receivedAt)
    {
    }
}

/// <summary>
/// 直播间开播.
/// </summary>
public sealed class LiveStartedEvent : LiveDanmakuEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveStartedEvent"/> class.
    /// </summary>
    /// <param name="command">触发的协议命令.</param>
    /// <param name="receivedAt">接收时间.</param>
    public LiveStartedEvent(string command, DateTimeOffset receivedAt)
        : base(command, receivedAt)
    {
    }
}

/// <summary>
/// 用户进入直播间.
/// </summary>
public sealed class LiveUserEnteredEvent : LiveDanmakuEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveUserEnteredEvent"/> class.
    /// </summary>
    /// <param name="user">进入用户.</param>
    /// <param name="command">触发的协议命令.</param>
    /// <param name="receivedAt">接收时间.</param>
    public LiveUserEnteredEvent(LiveUserEnteredInformation user, string command, DateTimeOffset receivedAt)
        : base(command, receivedAt)
        => User = user;

    /// <summary>
    /// 进入用户.
    /// </summary>
    public LiveUserEnteredInformation User { get; }
}

/// <summary>
/// 进入直播间的用户信息.
/// </summary>
public sealed class LiveUserEnteredInformation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveUserEnteredInformation"/> class.
    /// </summary>
    /// <param name="userId">用户 ID.</param>
    /// <param name="userName">昵称.</param>
    public LiveUserEnteredInformation(long userId, string userName)
    {
        UserId = userId;
        UserName = userName;
    }

    /// <summary>
    /// Gets 用户 ID.
    /// </summary>
    public long UserId { get; }

    /// <summary>
    /// Gets 昵称.
    /// </summary>
    public string UserName { get; }

    /// <summary>
    /// Gets 舰长等级.
    /// </summary>
    public int GuardLevel { get; init; }

    /// <summary>
    /// Gets 粉丝勋章名称.
    /// </summary>
    public string? MedalName { get; init; }

    /// <summary>
    /// Gets 粉丝勋章等级.
    /// </summary>
    public string? MedalLevel { get; init; }

    /// <summary>
    /// Gets 粉丝勋章颜色.
    /// </summary>
    public string? MedalColor { get; init; }

    /// <summary>
    /// Gets 昵称颜色.
    /// </summary>
    public string? NameColor { get; init; }

    /// <summary>
    /// Gets a value indicating whether 是否为管理员.
    /// </summary>
    public bool IsAdmin { get; init; }

    /// <summary>
    /// Gets a value indicating whether 是否为老爷.
    /// </summary>
    public bool IsVip { get; init; }
}

/// <summary>
/// 直播被切断.
/// </summary>
public sealed class LiveRoomCutOffEvent : LiveDanmakuEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveRoomCutOffEvent"/> class.
    /// </summary>
    /// <param name="message">服务端提示.</param>
    /// <param name="command">触发的协议命令.</param>
    /// <param name="receivedAt">接收时间.</param>
    public LiveRoomCutOffEvent(string? message, string command, DateTimeOffset receivedAt)
        : base(command, receivedAt)
        => Message = message;

    /// <summary>
    /// 服务端提示.
    /// </summary>
    public string? Message { get; }
}

/// <summary>
/// 播放地址需要重载.
/// </summary>
public sealed class LivePlayUrlReloadEvent : LiveDanmakuEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LivePlayUrlReloadEvent"/> class.
    /// </summary>
    /// <param name="rawJson">原始消息，供上层重取播放地址.</param>
    /// <param name="command">触发的协议命令.</param>
    /// <param name="receivedAt">接收时间.</param>
    public LivePlayUrlReloadEvent(string? rawJson, string command, DateTimeOffset receivedAt)
        : base(command, receivedAt)
        => RawJson = rawJson;

    /// <summary>
    /// 原始消息，供上层重取播放地址.
    /// </summary>
    public string? RawJson { get; }
}

/// <summary>
/// 连接状态变化.
/// </summary>
public sealed class LiveConnectionStateChangedEvent : LiveDanmakuEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveConnectionStateChangedEvent"/> class.
    /// </summary>
    /// <param name="state">新状态.</param>
    /// <param name="detail">状态说明.</param>
    /// <param name="receivedAt">发生时间.</param>
    public LiveConnectionStateChangedEvent(LiveDanmakuConnectionState state, string? detail, DateTimeOffset receivedAt)
        : base("CONNECTION_STATE", receivedAt)
    {
        State = state;
        Detail = detail;
    }

    /// <summary>
    /// 新状态.
    /// </summary>
    public LiveDanmakuConnectionState State { get; }

    /// <summary>
    /// 状态说明.
    /// </summary>
    public string? Detail { get; }
}

/// <summary>
/// 直播弹幕连接状态.
/// </summary>
public enum LiveDanmakuConnectionState
{
    /// <summary>
    /// 正在连接或认证.
    /// </summary>
    Connecting,

    /// <summary>
    /// 已连接并完成认证.
    /// </summary>
    Connected,

    /// <summary>
    /// 连接中断，正在重连.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// 已关闭.
    /// </summary>
    Closed,
}

/// <summary>
/// 在线人数数据来源.
/// </summary>
public enum LiveOnlineCountSource
{
    /// <summary>
    /// 心跳回包中的人气值.
    /// </summary>
    Popularity,

    /// <summary>
    /// WATCHED_CHANGE 中的看过人数.
    /// </summary>
    Watched,
}
