// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 心跳回执策略：每次心跳登记待回执，收到 op3 即清零；连续两次未回执判定连接失效.
/// </summary>
internal sealed class LiveHeartbeatPolicy
{
    /// <summary>
    /// 允许连续缺失的回执次数.
    /// </summary>
    public const int MaxPendingAcks = 2;

    /// <summary>
    /// 当前待回执次数.
    /// </summary>
    public int PendingAcks { get; private set; }

    /// <summary>
    /// 登记一次刚发出的心跳.
    /// </summary>
    /// <returns>达到缺失上限时返回 true，表示应判定连接失效.</returns>
    public bool RegisterHeartbeat()
    {
        PendingAcks++;
        return PendingAcks >= MaxPendingAcks;
    }

    /// <summary>
    /// 收到心跳回执（或迟到的回执），清零计数.
    /// </summary>
    public void OnAck() => PendingAcks = 0;
}
