// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.User.Core.Models;

/// <summary>
/// 用户空间信息响应.
/// </summary>
internal sealed class SpaceInfoResponse
{
    /// <summary>
    /// 用户 Id.
    /// </summary>
    [JsonPropertyName("mid")]
    public long UserId { get; set; }

    /// <summary>
    /// 昵称.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// 头像.
    /// </summary>
    [JsonPropertyName("face")]
    public string? Face { get; set; }

    /// <summary>
    /// 签名.
    /// </summary>
    [JsonPropertyName("sign")]
    public string? Sign { get; set; }

    /// <summary>
    /// 等级.
    /// </summary>
    [JsonPropertyName("level")]
    public int? Level { get; set; }

    /// <summary>
    /// 会员信息.
    /// </summary>
    [JsonPropertyName("vip")]
    public SpaceVipInformation? Vip { get; set; }
}

/// <summary>
/// 空间会员信息.
/// </summary>
internal sealed class SpaceVipInformation
{
    /// <summary>
    /// 会员状态.
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }
}

/// <summary>
/// 用户投稿列表响应.
/// </summary>
internal sealed class SpaceVideoResponse
{
    /// <summary>
    /// 视频列表容器.
    /// </summary>
    [JsonPropertyName("list")]
    public SpaceVideoList? List { get; set; }

    /// <summary>
    /// 分页信息.
    /// </summary>
    [JsonPropertyName("page")]
    public SpacePageInformation? Page { get; set; }
}

/// <summary>
/// 投稿列表.
/// </summary>
internal sealed class SpaceVideoList
{
    /// <summary>
    /// 视频列表.
    /// </summary>
    [JsonPropertyName("vlist")]
    public List<SpaceVideoItem>? Videos { get; set; }
}

/// <summary>
/// 投稿分页信息.
/// </summary>
internal sealed class SpacePageInformation
{
    /// <summary>
    /// 总数.
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }
}

/// <summary>
/// 投稿视频.
/// </summary>
internal sealed class SpaceVideoItem
{
    /// <summary>
    /// 视频 Aid.
    /// </summary>
    [JsonPropertyName("aid")]
    public long Aid { get; set; }

    /// <summary>
    /// 视频 Bvid.
    /// </summary>
    [JsonPropertyName("bvid")]
    public string? BvId { get; set; }

    /// <summary>
    /// 标题.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// 封面.
    /// </summary>
    [JsonPropertyName("pic")]
    public string? Cover { get; set; }

    /// <summary>
    /// 发布时间.
    /// </summary>
    [JsonPropertyName("created")]
    public long? Created { get; set; }

    /// <summary>
    /// 播放量.
    /// </summary>
    [JsonPropertyName("play")]
    public int PlayCount { get; set; }

    /// <summary>
    /// 弹幕量.
    /// </summary>
    [JsonPropertyName("video_review")]
    public int DanmakuCount { get; set; }

    /// <summary>
    /// 时长文本.
    /// </summary>
    [JsonPropertyName("length")]
    public string? Length { get; set; }
}

/// <summary>
/// 用户专栏列表响应.
/// </summary>
internal sealed class SpaceArticleResponse
{
    /// <summary>
    /// 专栏列表.
    /// </summary>
    [JsonPropertyName("articles")]
    public List<SpaceArticleItem>? Articles { get; set; }
}

/// <summary>
/// 空间专栏文章.
/// </summary>
internal sealed class SpaceArticleItem
{
    /// <summary>
    /// 文章 Id.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// 标题.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// 摘要.
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>
    /// 封面图.
    /// </summary>
    [JsonPropertyName("image_urls")]
    public List<string>? Images { get; set; }

    /// <summary>
    /// 发布时间.
    /// </summary>
    [JsonPropertyName("publish_time")]
    public long PublishTime { get; set; }
}