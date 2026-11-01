// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Content;
using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.Media.Core.Models;

/// <summary>
/// 番剧播放地址响应（v2 路径）.
/// </summary>
/// <remarks>
/// v2 在不同条件下会使用 <c>data.video_info</c> 或 <c>result.video_info</c> 两种外层包装，
/// 甚至直接返回 <c>data</c> / <c>result</c>；这里一并兼容，公开模型形状不变.
/// </remarks>
internal sealed class PgcPlayUrlResponse : BiliResponse
{
    /// <summary>
    /// data 包装.
    /// </summary>
    [JsonPropertyName("data")]
    public PgcPlayUrlContainer? Data { get; set; }

    /// <summary>
    /// result 包装.
    /// </summary>
    [JsonPropertyName("result")]
    public PgcPlayUrlContainer? Result { get; set; }

    /// <summary>
    /// 实际播放信息.
    /// </summary>
    public PlayerInformation? Content => Data?.VideoInfo ?? Result?.VideoInfo ?? Data?.VideoInfoDirect ?? Result?.VideoInfoDirect;
}

/// <summary>
/// 播放信息容器.
/// </summary>
internal sealed class PgcPlayUrlContainer
{
    /// <summary>
    /// 播放信息（video_info 包装）.
    /// </summary>
    [JsonPropertyName("video_info")]
    public PlayerInformation? VideoInfo { get; set; }

    /// <summary>
    /// 播放信息本体（无包装）.
    /// </summary>
    [JsonPropertyName("dash")]
    public DashVideo? Dash { get; set; }

    /// <summary>
    /// 播放信息本体.
    /// </summary>
    public PlayerInformation? VideoInfoDirect => Dash == null
        ? null
        : new PlayerInformation { VideoInformation = Dash };
}