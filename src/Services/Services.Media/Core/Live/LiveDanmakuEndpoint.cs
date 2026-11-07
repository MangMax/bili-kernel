// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 一次直播弹幕连接的端点信息.
/// </summary>
internal sealed class LiveDanmakuEndpoint
{
    /// <summary>
    /// 真实房间号.
    /// </summary>
    public string RoomId { get; init; } = string.Empty;

    /// <summary>
    /// 当前登录用户标识，未登录为 0.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 弹幕 token.
    /// </summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// buvid3.
    /// </summary>
    public string Buvid { get; init; } = string.Empty;

    /// <summary>
    /// 主机列表.
    /// </summary>
    public IReadOnlyList<LiveHost> Hosts { get; init; } = [];
}
