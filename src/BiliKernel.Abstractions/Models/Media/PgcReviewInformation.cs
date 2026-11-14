// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models.User;

namespace Richasy.BiliKernel.Models.Media;

/// <summary>
/// 影视作品评价（短评/长评）.
/// </summary>
public sealed class PgcReviewInformation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgcReviewInformation"/> class.
    /// </summary>
    /// <param name="user">作者.</param>
    /// <param name="content">内容.</param>
    /// <param name="title">标题，只有长评有.</param>
    /// <param name="score">评分.</param>
    /// <param name="likeCount">点赞数.</param>
    /// <param name="publishTime">发布时间.</param>
    /// <param name="progress">观看进度.</param>
    /// <param name="isShort">是否是短评.</param>
    public PgcReviewInformation(
        UserProfile user,
        string content,
        string? title,
        int? score,
        int? likeCount,
        DateTimeOffset? publishTime,
        string? progress,
        bool isShort)
    {
        User = user;
        Content = content;
        Title = title;
        Score = score;
        LikeCount = likeCount;
        PublishTime = publishTime;
        Progress = progress;
        IsShort = isShort;
    }

    /// <summary>
    /// 作者.
    /// </summary>
    public UserProfile User { get; }

    /// <summary>
    /// 内容.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// 标题，只有长评有.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// 评分.
    /// </summary>
    public int? Score { get; }

    /// <summary>
    /// 点赞数.
    /// </summary>
    public int? LikeCount { get; }

    /// <summary>
    /// 发布时间.
    /// </summary>
    public DateTimeOffset? PublishTime { get; }

    /// <summary>
    /// 观看进度.
    /// </summary>
    public string? Progress { get; }

    /// <summary>
    /// 是否是短评.
    /// </summary>
    public bool IsShort { get; }
}
