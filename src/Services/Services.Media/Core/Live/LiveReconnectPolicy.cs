// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 断线重连策略：正常关闭立即重连一次；异常按 1/2/4/8/16/30 秒退避；连续两次失败重新握手.
/// </summary>
internal sealed class LiveReconnectPolicy
{
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(16),
        TimeSpan.FromSeconds(30),
    ];

    private bool _immediateReconnectAvailable = true;
    private int _failuresSinceHandshake;

    /// <summary>
    /// 连续失败次数.
    /// </summary>
    public int ConsecutiveFailures { get; private set; }

    /// <summary>
    /// 连接断开后的重连决策.
    /// </summary>
    /// <param name="kind">断开类型.</param>
    /// <returns>重连决策.</returns>
    public LiveReconnectDecision OnDisconnected(LiveDisconnectKind kind)
    {
        if (kind == LiveDisconnectKind.Normal && _immediateReconnectAvailable)
        {
            _immediateReconnectAvailable = false;
            return new LiveReconnectDecision(TimeSpan.Zero, false, true);
        }

        return RegisterFailure();
    }

    /// <summary>
    /// 连接或认证以外的尝试失败.
    /// </summary>
    /// <returns>重连决策.</returns>
    public LiveReconnectDecision OnConnectionFailed() => RegisterFailure();

    /// <summary>
    /// 认证成功，重置失败与立即重连状态.
    /// </summary>
    public void OnAuthenticated()
    {
        ConsecutiveFailures = 0;
        _failuresSinceHandshake = 0;
        _immediateReconnectAvailable = true;
    }

    /// <summary>
    /// 重新握手完成，仅重置凭据刷新周期，保留连接退避.
    /// </summary>
    public void OnHandshakeCompleted() => _failuresSinceHandshake = 0;

    private LiveReconnectDecision RegisterFailure()
    {
        ConsecutiveFailures++;
        _failuresSinceHandshake++;
        var delay = Backoff[Math.Min(ConsecutiveFailures - 1, Backoff.Length - 1)];
        return new LiveReconnectDecision(delay, _failuresSinceHandshake >= 2, false);
    }
}

/// <summary>
/// 连接断开类型.
/// </summary>
internal enum LiveDisconnectKind
{
    /// <summary>服务端正常关闭.</summary>
    Normal,

    /// <summary>异常断开（网络错误、心跳失效、协议错误等）.</summary>
    Abnormal,
}

/// <summary>
/// 重连决策.
/// </summary>
/// <param name="Delay">重连前等待时间.</param>
/// <param name="RequiresHandshake">是否需要重新获取 token 与主机列表.</param>
/// <param name="ReconnectImmediately">是否立即重连.</param>
internal readonly record struct LiveReconnectDecision(TimeSpan Delay, bool RequiresHandshake, bool ReconnectImmediately);
