// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Services.Media.Core.Live;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 心跳、退避与主机轮换的纯策略测试.
/// </summary>
public sealed class LiveConnectionPolicyTests
{
    [Fact]
    public void TwoConsecutiveMissingHeartbeatAcksExpireConnection()
    {
        var policy = new LiveHeartbeatPolicy();

        Assert.False(policy.RegisterHeartbeat());
        Assert.True(policy.RegisterHeartbeat());
        Assert.Equal(2, policy.PendingAcks);
    }

    [Fact]
    public void LateHeartbeatAckClearsPendingCounter()
    {
        var policy = new LiveHeartbeatPolicy();
        policy.RegisterHeartbeat();

        policy.OnAck();

        Assert.Equal(0, policy.PendingAcks);
        Assert.False(policy.RegisterHeartbeat());
    }

    [Fact]
    public void AbnormallyClosedConnectionUsesExponentialBackoff()
    {
        var policy = new LiveReconnectPolicy();
        policy.OnAuthenticated();

        var delays = new List<TimeSpan>();
        var decision = policy.OnDisconnected(LiveDisconnectKind.Abnormal);
        delays.Add(decision.Delay);
        for (var i = 0; i < 6; i++)
        {
            delays.Add(policy.OnConnectionFailed().Delay);
        }

        Assert.Equal(
            [
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(4),
                TimeSpan.FromSeconds(8),
                TimeSpan.FromSeconds(16),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
            ],
            delays);
    }

    [Fact]
    public void NormalCloseReconnectsImmediatelyOnlyOnce()
    {
        var policy = new LiveReconnectPolicy();
        policy.OnAuthenticated();

        var first = policy.OnDisconnected(LiveDisconnectKind.Normal);
        var second = policy.OnDisconnected(LiveDisconnectKind.Normal);

        Assert.True(first.ReconnectImmediately);
        Assert.Equal(TimeSpan.Zero, first.Delay);
        Assert.False(second.ReconnectImmediately);
        Assert.Equal(TimeSpan.FromSeconds(1), second.Delay);
    }

    [Fact]
    public void TwoConsecutiveFailuresRequireHandshakeAndThenReset()
    {
        var policy = new LiveReconnectPolicy();
        policy.OnAuthenticated();

        var first = policy.OnConnectionFailed();
        var second = policy.OnConnectionFailed();

        Assert.False(first.RequiresHandshake);
        Assert.True(second.RequiresHandshake);

        policy.OnHandshakeCompleted();
        Assert.False(policy.OnConnectionFailed().RequiresHandshake);
    }

    [Fact]
    public void AuthenticationSuccessResetsFailureState()
    {
        var policy = new LiveReconnectPolicy();
        policy.OnConnectionFailed();
        policy.OnConnectionFailed();
        policy.OnDisconnected(LiveDisconnectKind.Normal);

        policy.OnAuthenticated();

        var decision = policy.OnDisconnected(LiveDisconnectKind.Abnormal);
        Assert.Equal(TimeSpan.FromSeconds(1), decision.Delay);
        Assert.False(decision.RequiresHandshake);
    }

    [Fact]
    public void HostPoolDeduplicatesAndRotates()
    {
        var pool = new LiveHostPool(
        [
            new LiveHost("chat.example.test", 443),
            new LiveHost("chat.example.test", 443),
            new LiveHost("chat2.example.test", 2245),
        ]);

        Assert.Equal(2, pool.Count);
        Assert.Equal("chat.example.test", pool.Next().HostName);
        Assert.Equal("chat2.example.test", pool.Next().HostName);
        Assert.Equal("chat.example.test", pool.Next().HostName);
    }

    [Fact]
    public void HostPoolResetKeepsRotationOffset()
    {
        var pool = new LiveHostPool(
        [
            new LiveHost("chat.example.test", 443),
            new LiveHost("chat2.example.test", 443),
        ]);
        _ = pool.Next();

        pool.Reset(
        [
            new LiveHost("chat.example.test", 443),
            new LiveHost("chat2.example.test", 443),
        ]);

        Assert.Equal("chat2.example.test", pool.Next().HostName);
    }

    [Fact]
    public void HostBuildsWebSocketUriOnDefaultPort()
    {
        // host_list 的 wss_port（如 2245）是普通 ws 端口，wss 必须走默认 443.
        var uri = new LiveHost("chat.example.test", 2245).ToUri();

        Assert.Equal("wss://chat.example.test/sub", uri.AbsoluteUri);
    }
}