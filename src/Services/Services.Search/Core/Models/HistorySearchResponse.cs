// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.Search.Core;

/// <summary>
/// 观看历史搜索（Web）响应.
/// </summary>
internal sealed class HistorySearchResponse
{
    /// <summary>
    /// 条目列表.
    /// </summary>
    [JsonPropertyName("list")]
    public List<HistorySearchItem>? List { get; set; }

    /// <summary>
    /// 总数.
    /// </summary>
    [JsonPropertyName("page")]
    public HistorySearchPage? Page { get; set; }
}

/// <summary>
/// 观看历史搜索分页.
/// </summary>
internal sealed class HistorySearchPage
{
    /// <summary>
    /// 总数.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// 观看历史搜索条目.
/// </summary>
internal sealed class HistorySearchItem
{
    /// <summary>
    /// 标题.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// 封面.
    /// </summary>
    [JsonPropertyName("cover")]
    public string? Cover { get; set; }

    /// <summary>
    /// 时长（秒）.
    /// </summary>
    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    /// <summary>
    /// 视频 Bvid.
    /// </summary>
    [JsonPropertyName("bvid")]
    public string? BvId { get; set; }

    /// <summary>
    /// 作者 Id.
    /// </summary>
    [JsonPropertyName("author_mid")]
    public long AuthorId { get; set; }

    /// <summary>
    /// 作者昵称.
    /// </summary>
    [JsonPropertyName("author_name")]
    public string? AuthorName { get; set; }

    /// <summary>
    /// 历史元信息.
    /// </summary>
    [JsonPropertyName("history")]
    public HistorySearchMeta? History { get; set; }
}

/// <summary>
/// 观看历史搜索元信息.
/// </summary>
internal sealed class HistorySearchMeta
{
    /// <summary>
    /// 业务主键.
    /// </summary>
    [JsonPropertyName("oid")]
    public long Oid { get; set; }

    /// <summary>
    /// 业务类型.
    /// </summary>
    [JsonPropertyName("business")]
    public string? Business { get; set; }
}