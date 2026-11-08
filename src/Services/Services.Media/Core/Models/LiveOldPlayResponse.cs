// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.Media.Core.Models;

/// <summary>
/// 游客旧播放数据（只返回 FLV 地址）.
/// </summary>
internal sealed class LiveOldPlayResponse
{
    /// <summary>
    /// 当前清晰度.
    /// </summary>
    [JsonPropertyName("current_qn")]
    public int CurrentQuality { get; set; }

    /// <summary>
    /// 播放地址列表.
    /// </summary>
    [JsonPropertyName("durl")]
    public List<LiveOldPlayDurl> Durl { get; set; }
}

/// <summary>
/// 游客旧播放地址.
/// </summary>
internal sealed class LiveOldPlayDurl
{
    /// <summary>
    /// 地址.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }
}
