// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.Media.Core.Models;

/// <summary>
/// 影视作品评价响应（短评与长评共用）.
/// </summary>
internal sealed class PgcReviewResponse
{
    /// <summary>
    /// 数据.
    /// </summary>
    [JsonPropertyName("data")]
    public PgcReviewData Data { get; set; }
}

/// <summary>
/// 影视作品评价数据.
/// </summary>
internal sealed class PgcReviewData
{
    /// <summary>
    /// 评价列表.
    /// </summary>
    [JsonPropertyName("list")]
    public List<PgcReviewItem> List { get; set; }

    /// <summary>
    /// 总数，长评接口用 count.
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>
    /// 总数，短评接口用 total.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// 单条评价.
/// </summary>
internal sealed class PgcReviewItem
{
    /// <summary>
    /// 评价 Id.
    /// </summary>
    [JsonPropertyName("review_id")]
    public long ReviewId { get; set; }

    /// <summary>
    /// 作者.
    /// </summary>
    [JsonPropertyName("author")]
    public PgcReviewAuthor Author { get; set; }

    /// <summary>
    /// 标题，只有长评有.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; }

    /// <summary>
    /// 内容.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; }

    /// <summary>
    /// 评分.
    /// </summary>
    [JsonPropertyName("score")]
    public int Score { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    [JsonPropertyName("ctime")]
    public long CTime { get; set; }

    /// <summary>
    /// 可读发布时间.
    /// </summary>
    [JsonPropertyName("push_time_str")]
    public string PushTimeStr { get; set; }

    /// <summary>
    /// 观看进度.
    /// </summary>
    [JsonPropertyName("progress")]
    public string Progress { get; set; }

    /// <summary>
    /// 互动数据.
    /// </summary>
    [JsonPropertyName("stat")]
    public PgcReviewStat Stat { get; set; }
}

/// <summary>
/// 评价作者.
/// </summary>
internal sealed class PgcReviewAuthor
{
    /// <summary>
    /// 头像.
    /// </summary>
    [JsonPropertyName("avatar")]
    public string Avatar { get; set; }

    /// <summary>
    /// 用户 Id.
    /// </summary>
    [JsonPropertyName("mid")]
    public long Mid { get; set; }

    /// <summary>
    /// 用户名.
    /// </summary>
    [JsonPropertyName("uname")]
    public string Name { get; set; }
}

/// <summary>
/// 评价互动数据.
/// </summary>
internal sealed class PgcReviewStat
{
    /// <summary>
    /// 点赞数.
    /// </summary>
    [JsonPropertyName("likes")]
    public int Likes { get; set; }
}
