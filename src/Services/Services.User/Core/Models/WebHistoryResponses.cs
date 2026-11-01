// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.User.Core.Models;

/// <summary>
/// 观看历史（Web）响应.
/// </summary>
internal sealed class WebHistoryResponse
{
    /// <summary>
    /// 历史条目.
    /// </summary>
    [JsonPropertyName("list")]
    public List<WebHistoryItem>? List { get; set; }

    /// <summary>
    /// 游标.
    /// </summary>
    [JsonPropertyName("cursor")]
    public WebHistoryCursor? Cursor { get; set; }
}

/// <summary>
/// 观看历史游标.
/// </summary>
internal sealed class WebHistoryCursor
{
    /// <summary>
    /// 下一页游标.
    /// </summary>
    [JsonPropertyName("max")]
    public long Max { get; set; }
}

/// <summary>
/// 观看历史条目.
/// </summary>
internal sealed class WebHistoryItem
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
    /// 历史元信息.
    /// </summary>
    [JsonPropertyName("history")]
    public WebHistoryMeta? History { get; set; }
}

/// <summary>
/// 观看历史元信息.
/// </summary>
internal sealed class WebHistoryMeta
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