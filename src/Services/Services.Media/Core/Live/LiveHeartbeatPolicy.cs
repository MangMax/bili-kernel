// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 心跳回执策略：连续两次超过回执期限时判定连接失效.
/// </summary>
internal sealed class LiveHeartbeatPolicy(TimeProvider timeProvider, TimeSpan ackTimeout)
{
    private readonly object _gate = new();
    private bool _pending;
    private long _sentAt;
    private int _missed;

    /// <summary>
    /// 检查上一心跳的期限，并在发送前登记新的待回执心跳.
    /// </summary>
    /// <returns>连续两次回执超时时返回 true，不再发送心跳.</returns>
    public bool RegisterHeartbeat()
    {
        lock (_gate)
        {
            var now = timeProvider.GetTimestamp();
            if (_pending)
            {
                if (timeProvider.GetElapsedTime(_sentAt, now) < ackTimeout)
                {
                    return false;
                }

                _missed++;
            }

            if (_missed >= 2)
            {
                return true;
            }

            _pending = true;
            _sentAt = now;
            return false;
        }
    }

    /// <summary>
    /// 收到回执（包括迟到回执），清除连续超时与待回执状态.
    /// </summary>
    public void OnAck()
    {
        lock (_gate)
        {
            _pending = false;
            _missed = 0;
        }
    }
}
