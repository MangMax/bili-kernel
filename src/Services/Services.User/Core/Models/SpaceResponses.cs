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

/// <summary>
/// 用户空间音频列表响应.
/// </summary>
internal sealed class SpaceAudioResponse
{
    /// <summary>
    /// 音频列表.
    /// </summary>
    [JsonPropertyName("data")]
    public List<SpaceAudioItem>? Items { get; set; }

    /// <summary>
    /// 页数.
    /// </summary>
    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }

    /// <summary>
    /// 总数.
    /// </summary>
    [JsonPropertyName("totalSize")]
    public int TotalSize { get; set; }
}

/// <summary>
/// 空间音频条目.
/// </summary>
internal sealed class SpaceAudioItem
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("cover")]
    public string? Cover { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("playNum")]
    public long? PlayCount { get; set; }
}

/// <summary>
/// 用户空间合集与系列列表响应.
/// </summary>
internal sealed class SpaceCollectionResponse
{
    /// <summary>
    /// 合集与系列.
    /// </summary>
    [JsonPropertyName("items_lists")]
    public SpaceCollectionLists? ItemsLists { get; set; }

    /// <summary>
    /// 分页信息.
    /// </summary>
    [JsonPropertyName("page")]
    public SpaceCollectionPage? Page { get; set; }
}

/// <summary>
/// 合集与系列列表.
/// </summary>
internal sealed class SpaceCollectionLists
{
    [JsonPropertyName("seasons_list")]
    public List<SpaceCollectionEntry>? Seasons { get; set; }

    [JsonPropertyName("series_list")]
    public List<SpaceCollectionEntry>? Series { get; set; }
}

/// <summary>
/// 合集或系列条目.
/// </summary>
internal sealed class SpaceCollectionEntry
{
    [JsonPropertyName("meta")]
    public SpaceCollectionMeta? Meta { get; set; }
}

/// <summary>
/// 合集或系列元信息.
/// </summary>
internal sealed class SpaceCollectionMeta
{
    [JsonPropertyName("season_id")]
    public long? SeasonId { get; set; }

    [JsonPropertyName("series_id")]
    public long? SeriesId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("cover")]
    public string? Cover { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("total")]
    public int? Total { get; set; }
}

/// <summary>
/// 合集与系列分页.
/// </summary>
internal sealed class SpaceCollectionPage
{
    [JsonPropertyName("page_num")]
    public int PageNumber { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// 合集或系列内的视频列表响应.
/// </summary>
internal sealed class SpaceArchivesResponse
{
    [JsonPropertyName("archives")]
    public List<SpaceArchiveItem>? Archives { get; set; }

    [JsonPropertyName("page")]
    public SpaceArchivePage? Page { get; set; }
}

/// <summary>
/// 合集或系列内的视频条目.
/// </summary>
internal sealed class SpaceArchiveItem
{
    [JsonPropertyName("aid")]
    public long Aid { get; set; }

    [JsonPropertyName("bvid")]
    public string? BvId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("pic")]
    public string? Cover { get; set; }

    [JsonPropertyName("duration")]
    public long? Duration { get; set; }

    [JsonPropertyName("pubdate")]
    public long? PublishTime { get; set; }

    [JsonPropertyName("stat")]
    public SpaceArchiveStat? Stat { get; set; }
}

/// <summary>
/// 合集内视频的统计数据.
/// </summary>
internal sealed class SpaceArchiveStat
{
    [JsonPropertyName("view")]
    public long? View { get; set; }

    [JsonPropertyName("danmaku")]
    public long? Danmaku { get; set; }
}

/// <summary>
/// 合集内视频分页.
/// </summary>
internal sealed class SpaceArchivePage
{
    [JsonPropertyName("page_num")]
    public int PageNumber { get; set; }

    [JsonPropertyName("num")]
    public int Number { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
