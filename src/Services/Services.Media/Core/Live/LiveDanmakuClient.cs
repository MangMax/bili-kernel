// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media.Core.Models;
using System.Globalization;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 直播弹幕会话：认证、心跳、收包、重连与事件流的状态机.
/// </summary>
internal sealed class LiveDanmakuClient : ILiveDanmakuSession
{
    private readonly ILiveDanmakuTransport _transport;
    private readonly TimeProvider _timeProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly TimeSpan _heartbeatInterval;
    private readonly TimeSpan _authTimeout;
    private readonly LiveReconnectPolicy _policy = new();
    private readonly LiveStatisticsCounters _counters = new();
    private readonly CancellationTokenSource _stopSource = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private LiveDanmakuEndpoint _endpoint;
    private LiveHostPool _hosts;
    private ILiveDanmakuSocket? _socket;
    private LiveHeartbeatPolicy? _heartbeat;
    private List<LiveDanmakuEvent> _authEvents = [];
    private int _enumerated;
    // 0 = not started, 1 = iterator owns cleanup, 2 = stopped before iteration.
    private int _streamState;
    private int _disposed;
    private int _state = (int)LiveDanmakuConnectionState.Connecting;

    public LiveDanmakuClient(
        ILiveDanmakuTransport transport,
        TimeProvider timeProvider,
        Func<TimeSpan, CancellationToken, Task> delay,
        TimeSpan heartbeatInterval,
        TimeSpan authTimeout,
        LiveDanmakuEndpoint endpoint)
    {
        _transport = transport;
        _timeProvider = timeProvider;
        _delay = delay;
        _heartbeatInterval = heartbeatInterval;
        _authTimeout = authTimeout;
        _endpoint = endpoint;
        _hosts = new LiveHostPool(endpoint.Hosts);
    }

    /// <inheritdoc/>
    public string RoomId => _endpoint.RoomId;

    /// <inheritdoc/>
    public LiveDanmakuStatistics Statistics => _counters.Snapshot();

    /// <inheritdoc/>
    public Task Completion => _completion.Task;

    /// <summary>
    /// 当前连接状态.
    /// </summary>
    public LiveDanmakuConnectionState State => (LiveDanmakuConnectionState)Volatile.Read(ref _state);

    /// <summary>
    /// 建立首个已认证连接.
    /// </summary>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>会话.</returns>
    public async Task<ILiveDanmakuSession> ConnectAsync(CancellationToken cancellationToken)
    {
        _socket = await ConnectWithRetryAsync(cancellationToken).ConfigureAwait(false);
        _policy.OnAuthenticated();
        SetState(LiveDanmakuConnectionState.Connected);
        return this;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<LiveDanmakuEvent> GetEventsAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _enumerated, 1) != 0)
        {
            throw new InvalidOperationException("直播弹幕事件流只能枚举一次");
        }

        return StreamEventsAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _stopSource.Cancel();
            if (Interlocked.CompareExchange(ref _streamState, 2, 0) == 0)
            {
                var socket = Interlocked.Exchange(ref _socket, null);
                await DisposeSocketAsync(socket).ConfigureAwait(false);
                SetState(LiveDanmakuConnectionState.Closed);
                _completion.TrySetResult();
            }
        }

        try
        {
            await _completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await StopAsync(timeout.Token).ConfigureAwait(false);
        _stopSource.Dispose();
    }

    private async IAsyncEnumerable<LiveDanmakuEvent> StreamEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _streamState, 1, 0) != 0)
        {
            yield break;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopSource.Token);
        var token = linked.Token;
        try
        {
            yield return new LiveConnectionStateChangedEvent(LiveDanmakuConnectionState.Connected, null, _timeProvider.GetUtcNow());
            while (!token.IsCancellationRequested)
            {
                var socket = _socket;
                if (socket is null)
                {
                    break;
                }

                foreach (var liveEvent in _authEvents)
                {
                    yield return liveEvent;
                }

                _authEvents.Clear();
                var closeKind = LiveSocketCloseKind.Abnormal;
                using (var connectionSource = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    _heartbeat = new LiveHeartbeatPolicy(_timeProvider, _heartbeatInterval);
                    var heartbeatTask = RunHeartbeatAsync(socket, connectionSource.Token);
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            LiveSocketReadResult read;
                            try
                            {
                                read = await socket.ReceiveAsync(connectionSource.Token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (token.IsCancellationRequested)
                            {
                                break;
                            }
                            catch (OperationCanceledException)
                            {
                                closeKind = LiveSocketCloseKind.Abnormal;
                                break;
                            }
                            catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException or LiveDanmakuProtocolException)
                            {
                                closeKind = LiveSocketCloseKind.Abnormal;
                                break;
                            }

                            if (read.Frame is not { } frame)
                            {
                                closeKind = read.CloseKind == LiveSocketCloseKind.Normal
                                    ? LiveSocketCloseKind.Normal
                                    : LiveSocketCloseKind.Abnormal;
                                break;
                            }

                            using (frame)
                            {
                                _counters.AddFrame();
                                List<LiveDanmakuEvent> events;
                                try
                                {
                                    events = Decode(frame.Span);
                                }
                                catch (LiveDanmakuProtocolException)
                                {
                                    closeKind = LiveSocketCloseKind.Abnormal;
                                    break;
                                }

                                foreach (var liveEvent in events)
                                {
                                    yield return liveEvent;
                                }
                            }
                        }
                    }
                    finally
                    {
                        connectionSource.Cancel();
                        try
                        {
                            await heartbeatTask.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }

                        _heartbeat = null;
                        _socket = null;
                        await DisposeSocketAsync(socket).ConfigureAwait(false);
                    }
                }

                if (token.IsCancellationRequested)
                {
                    break;
                }

                _counters.AddReconnect();
                SetState(LiveDanmakuConnectionState.Reconnecting);
                yield return new LiveConnectionStateChangedEvent(
                    LiveDanmakuConnectionState.Reconnecting,
                    closeKind == LiveSocketCloseKind.Normal ? "服务端关闭连接" : "连接异常断开",
                    _timeProvider.GetUtcNow());

                var disconnectKind = closeKind == LiveSocketCloseKind.Normal
                    ? LiveDisconnectKind.Normal
                    : LiveDisconnectKind.Abnormal;
                var decision = _policy.OnDisconnected(disconnectKind);
                if (decision.RequiresHandshake)
                {
                    await RefreshEndpointAsync(token).ConfigureAwait(false);
                }

                if (decision.Delay > TimeSpan.Zero)
                {
                    await _delay(decision.Delay, token).ConfigureAwait(false);
                }

                if (token.IsCancellationRequested)
                {
                    break;
                }

                _socket = await ConnectWithRetryAsync(token).ConfigureAwait(false);
                _policy.OnAuthenticated();
                SetState(LiveDanmakuConnectionState.Connected);
                yield return new LiveConnectionStateChangedEvent(LiveDanmakuConnectionState.Connected, null, _timeProvider.GetUtcNow());
            }
        }
        finally
        {
            await DisposeSocketAsync(Interlocked.Exchange(ref _socket, null)).ConfigureAwait(false);
            SetState(LiveDanmakuConnectionState.Closed);
            _completion.TrySetResult();
        }
    }

    private async Task<ILiveDanmakuSocket> ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var host = _hosts.Next();
            ILiveDanmakuSocket? socket = null;
            try
            {
                socket = await _transport.ConnectAsync(host.ToUri(), cancellationToken).ConfigureAwait(false);
                await AuthenticateAsync(socket, cancellationToken).ConfigureAwait(false);
                return socket;
            }
            catch (LiveDanmakuUnauthorizedException)
            {
                await DisposeSocketAsync(socket).ConfigureAwait(false);
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await DisposeSocketAsync(socket).ConfigureAwait(false);
                throw;
            }
            catch (Exception)
            {
                await DisposeSocketAsync(socket).ConfigureAwait(false);
                var decision = _policy.OnConnectionFailed();
                if (decision.RequiresHandshake)
                {
                    await RefreshEndpointAsync(cancellationToken).ConfigureAwait(false);
                }

                if (decision.Delay > TimeSpan.Zero)
                {
                    await _delay(decision.Delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task AuthenticateAsync(ILiveDanmakuSocket socket, CancellationToken cancellationToken)
    {
        var message = new EnterRoomMessage
        {
            roomid = int.TryParse(_endpoint.RoomId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var roomId) ? roomId : 0,
            uid = _endpoint.UserId,
            buvid = _endpoint.Buvid,
            protover = 3,
            key = _endpoint.Token,
            platform = "web",
            type = 2,
            clientver = "1.4.3",
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(message, SourceGenerationContext.Default.EnterRoomMessage);
        await socket.SendAsync(LivePacketCodec.Encode(7, 1, body), cancellationToken).ConfigureAwait(false);
        await WaitForAuthAckAsync(socket, cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForAuthAckAsync(ILiveDanmakuSocket socket, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveTask = ReceiveAuthReplyAsync(socket, timeoutSource.Token);
        try
        {
            await receiveTask.WaitAsync(_authTimeout, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            timeoutSource.Cancel();
            Observe(receiveTask);
            throw new TimeoutException("认证超时");
        }
    }

    private async Task ReceiveAuthReplyAsync(ILiveDanmakuSocket socket, CancellationToken cancellationToken)
    {
        while (true)
        {
            var read = await socket.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (read.Frame is not { } frame)
            {
                if (read.CloseKind == LiveSocketCloseKind.Normal)
                {
                    throw new LiveDanmakuProtocolException("认证前连接被服务端关闭");
                }

                throw new LiveDanmakuProtocolException("认证前连接异常断开");
            }

            using (frame)
            {
                int? code = null;
                string? message = null;
                var result = LivePacketCodec.Decode(frame.Span, (operation, _, _, payload) =>
                {
                    if (operation == 8 && LiveMessageParser.TryParseAuthReply(payload, out var replyCode, out var replyMessage))
                    {
                        code = replyCode;
                        message = replyMessage;
                    }
                });

                if (result.Status != LiveDecodeStatus.Success)
                {
                    throw new LiveDanmakuProtocolException($"认证回执解码失败：{result.Status}");
                }

                if (code.HasValue)
                {
                    if (code.Value == 0)
                    {
                        _authEvents = Decode(frame.Span);
                        return;
                    }

                    throw new LiveDanmakuUnauthorizedException(code.Value, message);
                }
            }
        }
    }

    private async Task RunHeartbeatAsync(ILiveDanmakuSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(_heartbeatInterval, _timeProvider);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_heartbeat?.RegisterHeartbeat() == true)
                {
                    socket.Abort();
                    return;
                }

                try
                {
                    await socket.SendAsync(LivePacketCodec.Encode(2, 1, ReadOnlySpan<byte>.Empty), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException or OperationCanceledException)
                {
                    socket.Abort();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private List<LiveDanmakuEvent> Decode(ReadOnlySpan<byte> buffer)
    {
        var events = new List<LiveDanmakuEvent>();
        var receivedAt = _timeProvider.GetUtcNow();
        var result = LivePacketCodec.Decode(buffer, (operation, _, _, payload) =>
        {
            if (operation == 5)
            {
                var kind = LiveMessageParser.Parse(payload, receivedAt, out var liveEvent);
                switch (kind)
                {
                    case LiveMessageParseKind.Event:
                        _counters.AddParsed();
                        events.Add(liveEvent!);
                        break;
                    case LiveMessageParseKind.Unknown:
                        _counters.AddUnknown();
                        break;
                    case LiveMessageParseKind.Ignored:
                        _counters.AddIgnored();
                        break;
                    default:
                        _counters.AddMalformed();
                        break;
                }
            }
            else if (operation == 3)
            {
                _heartbeat?.OnAck();
                if (LiveMessageParser.TryParseHeartbeatReply(payload, out var onlineCount))
                {
                    if (onlineCount is { } count)
                    {
                        _counters.AddParsed();
                        events.Add(new LiveOnlineCountChangedEvent(count, LiveOnlineCountSource.Popularity, null, "HEARTBEAT_REPLY", receivedAt));
                    }
                    else
                    {
                        _counters.AddSkipped();
                    }
                }
                else
                {
                    _counters.AddMalformed();
                }
            }
            else
            {
                // 心跳、认证回执、关闭等控制包不需要产出事件.
                _counters.AddSkipped();
            }
        });

        _counters.AddDecoded(result.PacketCount);
        _counters.AddSkipped(result.ContainerPackets + result.SkippedFrames);
        if (result.Status != LiveDecodeStatus.Success)
        {
            throw new LiveDanmakuProtocolException($"帧解析失败：{result.Status}");
        }

        return events;
    }

    private async Task RefreshEndpointAsync(CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = await _transport.ResolveEndpointAsync(_endpoint.RoomId, cancellationToken).ConfigureAwait(false);
            _endpoint = endpoint;
            _hosts.Reset(endpoint.Hosts);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // 重新握手失败时保留旧端点，继续按退避重试.
        }
        finally
        {
            _policy.OnHandshakeCompleted();
        }
    }

    private void SetState(LiveDanmakuConnectionState state) => Volatile.Write(ref _state, (int)state);

    private static async Task DisposeSocketAsync(ILiveDanmakuSocket? socket)
    {
        if (socket is null)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await socket.CloseAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 对端已断开时关闭会失败，继续释放.
        }

        try
        {
            await socket.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private static void Observe(Task task)
        => _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}