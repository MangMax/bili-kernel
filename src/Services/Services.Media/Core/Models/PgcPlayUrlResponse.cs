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
    public PlayerInformation? Content => Container?.VideoInfo ?? Container?.VideoInfoDirect;

    /// <summary>
    /// 播放校验标记，与 <see cref="Content"/> 来自同一外层包装.
    /// </summary>
    public PgcPlayCheck? PlayCheck => Container?.PlayCheck;

    /// <summary>
    /// 观看权益信息，与 <see cref="Content"/> 来自同一外层包装.
    /// </summary>
    public PgcPlayViewInfo? ViewInfo => Container?.ViewInfo;

    private PgcPlayUrlContainer? Container => Data?.VideoInfo != null || Data?.Dash != null || Data?.PlayCheck != null || Data?.ViewInfo != null
        ? Data
        : Result;
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

    /// <summary>
    /// 播放校验标记.
    /// </summary>
    [JsonPropertyName("play_check")]
    public PgcPlayCheck? PlayCheck { get; set; }

    /// <summary>
    /// 观看权益信息.
    /// </summary>
    [JsonPropertyName("view_info")]
    public PgcPlayViewInfo? ViewInfo { get; set; }
}

/// <summary>
/// 播放校验标记.
/// </summary>
internal sealed class PgcPlayCheck
{
    /// <summary>
    /// 校验结果，如 PLAY_WHOLE / PLAY_PREVIEW.
    /// </summary>
    [JsonPropertyName("play_detail")]
    public string? PlayDetail { get; set; }
}

/// <summary>
/// 观看权益信息.
/// </summary>
internal sealed class PgcPlayViewInfo
{
    /// <summary>
    /// 权益提示弹窗.
    /// </summary>
    [JsonPropertyName("dialog")]
    public PgcPlayDialog? Dialog { get; set; }
}

/// <summary>
/// 权益提示弹窗.
/// </summary>
internal sealed class PgcPlayDialog
{
    /// <summary>
    /// 弹窗类型，pay 表示需要购买或开通大会员.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}