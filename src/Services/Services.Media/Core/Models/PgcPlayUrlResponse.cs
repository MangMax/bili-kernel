// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.Media.Core.Models;

/// <summary>
/// 番剧播放地址响应（v2 路径）.
/// </summary>
internal sealed class PgcPlayUrlResponse
{
    /// <summary>
    /// 播放信息.
    /// </summary>
    [JsonPropertyName("video_info")]
    public PlayerInformation VideoInfo { get; set; }
}