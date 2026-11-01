// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Content;
using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.Media.Core.Models;

/// <summary>
/// 番剧详情响应包装，Web 端点放在 result，旧版 App 端点放在 data.
/// </summary>
internal sealed class PgcDetailEnvelope : BiliResponse
{
    /// <summary>
    /// 旧版数据.
    /// </summary>
    [JsonPropertyName("data")]
    public PgcDetailResponse? Data { get; set; }

    /// <summary>
    /// 现行数据.
    /// </summary>
    [JsonPropertyName("result")]
    public PgcDetailResponse? Result { get; set; }

    /// <summary>
    /// 实际内容.
    /// </summary>
    public PgcDetailResponse? Content => Result ?? Data;
}

/// <summary>
/// 番剧时间线响应包装.
/// </summary>
internal sealed class PgcTimeLineEnvelope : BiliResponse
{
    /// <summary>
    /// 旧版数据.
    /// </summary>
    [JsonPropertyName("data")]
    public PgcTimeLineResponse? Data { get; set; }

    /// <summary>
    /// 现行数据.
    /// </summary>
    [JsonPropertyName("result")]
    public PgcTimeLineResponse? Result { get; set; }

    /// <summary>
    /// 实际内容.
    /// </summary>
    public PgcTimeLineResponse? Content => Result ?? Data;
}

/// <summary>
/// 分区视频归档响应.
/// </summary>
internal sealed class PartitionArchiveResponse
{
    /// <summary>
    /// 视频列表.
    /// </summary>
    [JsonPropertyName("archives")]
    public List<PartitionVideo>? Archives { get; set; }
}