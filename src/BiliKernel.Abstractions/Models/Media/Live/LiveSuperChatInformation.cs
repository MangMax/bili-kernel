// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Models.Media;

/// <summary>
/// 直播醒目留言.
/// </summary>
public sealed class LiveSuperChatInformation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveSuperChatInformation"/> class.
    /// </summary>
    /// <param name="id">留言标识.</param>
    /// <param name="message">留言内容.</param>
    /// <param name="price">金额.</param>
    /// <param name="userName">用户名.</param>
    /// <param name="userId">用户标识.</param>
    /// <param name="startTime">开始时间.</param>
    /// <param name="endTime">结束时间.</param>
    /// <param name="backgroundColor">背景色.</param>
    public LiveSuperChatInformation(
        string id,
        string message,
        int price,
        string userName,
        long userId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? backgroundColor)
    {
        Id = id;
        Message = message;
        Price = price;
        UserName = userName;
        UserId = userId;
        StartTime = startTime;
        EndTime = endTime;
        BackgroundColor = backgroundColor;
    }

    /// <summary>
    /// 留言标识.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 留言内容.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 金额.
    /// </summary>
    public int Price { get; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; }

    /// <summary>
    /// 用户标识.
    /// </summary>
    public long UserId { get; }

    /// <summary>
    /// 开始时间.
    /// </summary>
    public DateTimeOffset StartTime { get; }

    /// <summary>
    /// 结束时间.
    /// </summary>
    public DateTimeOffset EndTime { get; }

    /// <summary>
    /// 背景色.
    /// </summary>
    public string? BackgroundColor { get; }
}
