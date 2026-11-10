// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media;
using Richasy.BiliKernel.Services.Media.Core.Live;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 直播弹幕服务的连接、认证、心跳、重连与取消测试（全部内存，不联网）.
/// </summary>
public sealed class LiveDanmakuServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ConnectSendsProtocolVersionThreeAuthPacketAndAwaitsAck()
    {
        var socket = new FakeLiveSocket(authCode: 0);
        var transport = CreateTransport(socket);
        var session = await CreateService(transport).ConnectAsync("1234").ConfigureAwait(true);

        var auth = Assert.Single(socket.SentPackets, p => p.Operation == 7);
        Assert.Equal(1, auth.Version);
        using var body = JsonDocument.Parse(auth.Payload);
        Assert.Equal(1234, body.RootElement.GetProperty("roomid").GetInt32());
        Assert.Equal(42, body.RootElement.GetProperty("uid").GetInt64());
        Assert.Equal(3, body.RootElement.GetProperty("protover").GetInt32());
        Assert.Equal("web", body.RootElement.GetProperty("platform").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("type").GetInt32());
        Assert.Equal("token", body.RootElement.GetProperty("key").GetString());
        Assert.Equal("B3", body.RootElement.GetProperty("buvid").GetString());

        // 心跳必须在认证成功后才开始.
        Assert.DoesNotContain(socket.SentPackets, p => p.Operation == 2);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task AuthenticationFailureIsExplicitAndNotRetried()
    {
        var socket = new FakeLiveSocket(authCode: -101);
        var transport = CreateTransport(socket);

        var error = await Assert.ThrowsAsync<LiveDanmakuUnauthorizedException>(
            () => CreateService(transport).ConnectAsync("1234"));

        Assert.Equal(-101, error.Code);
        Assert.Equal(1, transport.ConnectCount);
        Assert.Equal(1, transport.ResolveCount);
        Assert.Empty(transport.Delays);
    }

    [Fact]
    public async Task FirstEventIsConnectedStateThenDanmaku()
    {
        var socket = new FakeLiveSocket(authCode: 0);
        var transport = CreateTransport(socket);
        var session = await CreateService(transport).ConnectAsync("1234").ConfigureAwait(true);

        socket.Enqueue(Frame(Utf8("""{"cmd":"DANMU_MSG","info":[[0,1,25,16777215],"hello",[7,"Alice",0,0,0],[],[],[]]}""")));

        var events = await ReadEventsAsync(session, 2);

        var state = Assert.IsType<LiveConnectionStateChangedEvent>(events[0]);
        Assert.Equal(LiveDanmakuConnectionState.Connected, state.State);
        Assert.IsType<LiveDanmakuReceivedEvent>(events[1]);
        Assert.Equal(1, session.Statistics.ParsedMessages);
        Assert.Equal(0, session.Statistics.UnknownCommands);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task UnknownCommandIsCountedAndDoesNotBreakStream()
    {
        var socket = new FakeLiveSocket(authCode: 0);
        var transport = CreateTransport(socket);
        var session = await CreateService(transport).ConnectAsync("1234").ConfigureAwait(true);
        socket.Enqueue(Frame(Utf8("""{"cmd":"FUTURE_COMMAND"}""")));
        socket.Enqueue(Frame(Utf8("""{"cmd":"PREPARING"}""")));

        var events = await ReadEventsAsync(session, 2);

        var statistics = session.Statistics;
        Assert.Equal(1, statistics.UnknownCommands);
        Assert.Equal(
            statistics.DecodedPackets,
            statistics.ParsedMessages + statistics.UnknownCommands + statistics.MalformedMessages + statistics.IgnoredMessages + statistics.SkippedPackets);
        Assert.IsType<LiveRoomNotStartedEvent>(events[1]);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task NormalCloseReconnectsImmediatelyWithoutBackoff()
    {
        var first = new FakeLiveSocket(authCode: 0);
        first.EnqueueCloseAfterAuth(LiveSocketCloseKind.Normal);
        var second = new FakeLiveSocket(authCode: 0);
        var transport = CreateTransport(first, second);
        var session = await CreateService(transport).ConnectAsync("1234").ConfigureAwait(true);

        _ = await ReadEventsAsync(session, 3);

        Assert.Equal(2, transport.ConnectCount);
        Assert.Empty(transport.Delays);
        Assert.Equal(1, session.Statistics.Reconnects);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task ConsecutiveConnectFailuresRefreshEndpointAndRotateHost()
    {
        var socket = new FakeLiveSocket(authCode: 0);
        var transport = CreateTransport([new IOException("boom"), new IOException("boom"), socket]);
        var session = await CreateService(transport).ConnectAsync("1234").ConfigureAwait(true);

        Assert.Equal(3, transport.ConnectCount);
        Assert.Equal(2, transport.ResolveCount);
        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)],
            transport.Delays);
        Assert.Equal(
            ["chat.example.test", "chat2.example.test", "chat.example.test"],
            transport.Uris.Select(u => u.Host));
        await session.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task MissingTwoHeartbeatAcksTriggersReconnect()
    {
        var first = new FakeLiveSocket(authCode: 0);
        var second = new FakeLiveSocket(authCode: 0);
        var transport = CreateTransport(first, second);
        var session = await CreateService(transport, heartbeatInterval: TimeSpan.FromMilliseconds(20))
            .ConnectAsync("1234").ConfigureAwait(true);
        using var pumpSource = new CancellationTokenSource(Timeout);
        var pump = Task.Run(async () =>
        {
            await foreach (var _ in session.GetEventsAsync(pumpSource.Token).ConfigureAwait(false))
            {
            }
        });

        await WaitUntilAsync(() => transport.ConnectCount >= 2, Timeout);

        Assert.True(first.Aborted);
        Assert.Equal(1, session.Statistics.Reconnects);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task TruncatedFrameAbortsCurrentConnectionAndReconnects()
    {
        var first = new FakeLiveSocket(authCode: 0);
        var mangled = Frame(Utf8("{}"));
        BinaryPrimitives.WriteInt32BigEndian(mangled.AsSpan(0, 4), 4096);
        first.EnqueueAfterAuth(mangled);
        var second = new FakeLiveSocket(authCode: 0);
        var transport = CreateTransport(first, second);
        var session = await CreateService(transport).ConnectAsync("1234").ConfigureAwait(true);

        var events = await ReadEventsAsync(session, 3);

        Assert.Equal(2, transport.ConnectCount);
        Assert.IsType<LiveConnectionStateChangedEvent>(events[1]);
        Assert.Equal(LiveDanmakuConnectionState.Reconnecting, ((LiveConnectionStateChangedEvent)events[1]).State);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task CancellationEndsEventsAndBackgroundLoopsWithinBoundedTime()
    {
        var socket = new FakeLiveSocket(authCode: 0);
        var transport = CreateTransport(socket);
        var session = await CreateService(transport).ConnectAsync("1234");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var started = DateTimeOffset.UtcNow;
        await foreach (var _ in session.GetEventsAsync(cts.Token).ConfigureAwait(true))
        {
        }

        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(2));
        await session.Completion.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
        await session.DisposeAsync().ConfigureAwait(true);
        await session.DisposeAsync().ConfigureAwait(true);
        Assert.NotNull(session.Statistics);
        Assert.Equal(1, socket.DisposeCount);
        Assert.Equal(1, socket.CloseCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task BreakingEventEnumerationDisposesSocketBeforeCompletion(int eventCount)
    {
        var socket = new FakeLiveSocket(authCode: 0);
        socket.EnqueueAfterAuth(Frame(Utf8("""{"cmd":"PREPARING"}""")));
        var session = await CreateService(CreateTransport(socket)).ConnectAsync("1234");
        _ = await ReadEventsAsync(session, eventCount);
        await session.Completion.WaitAsync(Timeout);
        Assert.Equal(1, socket.DisposeCount);
        Assert.Equal(1, socket.CloseCount);
        await session.DisposeAsync();
        Assert.Equal(1, socket.DisposeCount);
    }

    [Fact]
    public async Task StopBeforeEnumerationStartsDisposesSocketAndCompletesSession()
    {
        var socket = new FakeLiveSocket(authCode: 0);
        var session = await CreateService(CreateTransport(socket)).ConnectAsync("1234");
        var events = session.GetEventsAsync();
        await session.StopAsync().AsTask().WaitAsync(Timeout);
        Assert.True(session.Completion.IsCompletedSuccessfully);
        Assert.Equal(1, socket.DisposeCount);
        await using var enumerator = events.GetAsyncEnumerator();
        Assert.False(await enumerator.MoveNextAsync());
        await session.DisposeAsync();
        Assert.Equal(1, socket.DisposeCount);
    }

    [Fact]
    public async Task UnexpectedReceiveFailureDisposesSocketAndCompletesSession()
    {
        var socket = new FakeLiveSocket(authCode: 0)
        {
            ReceiveFailureAfterAuth = new InvalidOperationException("unexpected failure"),
        };
        var session = await CreateService(CreateTransport(socket)).ConnectAsync("1234");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadEventsAsync(session, 2));
        await session.Completion.WaitAsync(Timeout);
        Assert.Equal(1, socket.DisposeCount);
        Assert.Equal(1, socket.CloseCount);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task ReceiveProtocolLimitFailureReconnectsAndDisposesBothSockets()
    {
        var first = new FakeLiveSocket(authCode: 0)
        {
            ReceiveFailureAfterAuth = new LiveDanmakuProtocolException("消息长度超过接收上限"),
        };
        var second = new FakeLiveSocket(authCode: 0);
        var transport = CreateTransport(first, second);
        var session = await CreateService(transport).ConnectAsync("1234");
        var events = await ReadEventsAsync(session, 3);
        Assert.Equal(LiveDanmakuConnectionState.Reconnecting, Assert.IsType<LiveConnectionStateChangedEvent>(events[1]).State);
        Assert.Equal(LiveDanmakuConnectionState.Connected, Assert.IsType<LiveConnectionStateChangedEvent>(events[2]).State);
        Assert.Equal(2, transport.ConnectCount);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task RepeatedConnectionFailuresKeepIncreasingBackoffAcrossRefreshes()
    {
        var socket = new FakeLiveSocket(authCode: 0);
        var attempts = Enumerable.Range(0, 7).Select(_ => (object)new IOException("offline")).Append(socket).ToArray();
        var transport = CreateTransport(attempts);
        var session = await CreateService(transport).ConnectAsync("1234");
        int[] seconds = [1, 2, 4, 8, 16, 30, 30];
        Assert.Equal(seconds.Select(value => TimeSpan.FromSeconds(value)), transport.Delays);
        Assert.Equal(4, transport.ResolveCount);
        Assert.Equal(8, transport.ConnectCount);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task AuthenticationAckPreservesBusinessSiblingInSameFrame()
    {
        var socket = new FakeLiveSocket(authCode: 0)
        {
            AuthSibling = Frame(Utf8("""{"cmd":"PREPARING"}""")),
        };
        var session = await CreateService(CreateTransport(socket)).ConnectAsync("1234");
        var events = await ReadEventsAsync(session, 2);
        Assert.IsType<LiveRoomNotStartedEvent>(events[1]);
        Assert.Equal(1, session.Statistics.ParsedMessages);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task AuthenticationTimeoutRetriesInsteadOfReportingUnauthorized()
    {
        var first = new FakeLiveSocket(authCode: 0) { SuppressAuthReply = true };
        var second = new FakeLiveSocket(authCode: 0);
        var transport = CreateTransport(first, second);
        var endpoint = await transport.ResolveEndpointAsync("1234", CancellationToken.None);
        var client = new LiveDanmakuClient(transport, TimeProvider.System, transport.DelayAsync,
            TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(20), endpoint);
        var session = await client.ConnectAsync(CancellationToken.None).WaitAsync(Timeout);
        Assert.Equal(2, transport.ConnectCount);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(TimeSpan.FromSeconds(1), Assert.Single(transport.Delays));
        await session.DisposeAsync();
    }

    [Fact]
    public async Task AckReceivedBeforeSendCompletesDoesNotBecomeAMissedHeartbeat()
    {
        var ackObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var socket = new FakeLiveSocket(authCode: 0);
        var sends = 0;
        socket.OnHeartbeat = async token =>
        {
            var count = Interlocked.Increment(ref sends);
            if (count == 1)
            {
                socket.Enqueue(Frame([0, 0, 0, 7], operation: 3));
                await ackObserved.Task.WaitAsync(token).ConfigureAwait(false);
            }
            else if (count == 3)
            {
                thirdSent.TrySetResult();
                await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
            }
        };
        var transport = CreateTransport(socket);
        var session = await CreateService(transport, TimeSpan.FromMilliseconds(100)).ConnectAsync("1234");
        using var cancellation = new CancellationTokenSource(Timeout);
        var pump = Task.Run(async () =>
        {
            await foreach (var liveEvent in session.GetEventsAsync(cancellation.Token).ConfigureAwait(false))
            {
                if (liveEvent is LiveOnlineCountChangedEvent)
                {
                    ackObserved.TrySetResult();
                }
            }
        });
        try
        {
            await thirdSent.Task.WaitAsync(Timeout);
            Assert.False(socket.Aborted);
            Assert.Equal(1, transport.ConnectCount);
        }
        finally
        {
            cancellation.Cancel();
            await pump.WaitAsync(Timeout);
            await session.DisposeAsync();
        }
    }

    private static LiveDanmakuService CreateService(
        FakeLiveTransport transport,
        TimeSpan? heartbeatInterval = null)
        => new(transport, heartbeatInterval, TimeProvider.System, transport.DelayAsync);

    private static FakeLiveTransport CreateTransport(params FakeLiveSocket[] sockets)
        => CreateTransport(sockets.Cast<object>().ToArray());

    private static FakeLiveTransport CreateTransport(params object[] attempts)
        => new(new LiveDanmakuEndpoint
        {
            RoomId = "1234",
            UserId = 42,
            Token = "token",
            Buvid = "B3",
            Hosts =
            [
                new LiveHost("chat.example.test", 443),
                new LiveHost("chat2.example.test", 443),
            ],
        }, attempts);

    private static async Task<List<LiveDanmakuEvent>> ReadEventsAsync(ILiveDanmakuSession session, int count)
    {
        var events = new List<LiveDanmakuEvent>();
        using var cts = new CancellationTokenSource(Timeout);
        await foreach (var liveEvent in session.GetEventsAsync(cts.Token).ConfigureAwait(true))
        {
            events.Add(liveEvent);
            if (events.Count == count)
            {
                break;
            }
        }

        return events;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var started = DateTimeOffset.UtcNow;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow - started > timeout)
            {
                throw new TimeoutException("条件在限定时间内未满足");
            }

            await Task.Delay(10).ConfigureAwait(true);
        }
    }

    private static byte[] Frame(byte[] body, int operation = 5, int version = 0)
    {
        var bytes = new byte[16 + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(0, 4), bytes.Length);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4, 2), 16);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6, 2), (ushort)version);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8, 4), operation);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(12, 4), 1);
        body.CopyTo(bytes.AsSpan(16));
        return bytes;
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}

internal sealed class FakeLiveTransport(LiveDanmakuEndpoint endpoint, object[] attempts) : ILiveDanmakuTransport
{
    private readonly List<Uri> _uris = [];
    private readonly List<TimeSpan> _delays = [];

    public int ResolveCount { get; private set; }

    public int ConnectCount { get; private set; }

    public IReadOnlyList<Uri> Uris => _uris;

    public IReadOnlyList<TimeSpan> Delays => _delays;

    public Task<LiveDanmakuEndpoint> ResolveEndpointAsync(string roomId, CancellationToken cancellationToken)
    {
        ResolveCount++;
        return Task.FromResult(endpoint);
    }

    public Task<ILiveDanmakuSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        ConnectCount++;
        _uris.Add(uri);
        var attempt = attempts[ConnectCount - 1];
        if (attempt is Exception exception)
        {
            return Task.FromException<ILiveDanmakuSocket>(exception);
        }

        return Task.FromResult<ILiveDanmakuSocket>((FakeLiveSocket)attempt);
    }

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        _delays.Add(delay);
        return Task.CompletedTask;
    }
}

internal sealed class FakeLiveSocket(int authCode) : ILiveDanmakuSocket
{
    private readonly Channel<LiveSocketReadResult> _incoming = Channel.CreateUnbounded<LiveSocketReadResult>();
    private readonly List<SentPacket> _sent = [];
    private readonly List<int> _receivedOperations = [];
    private readonly List<LiveSocketReadResult> _afterAuth = [];
    private readonly List<byte[]> _afterAuthFrames = [];

    public bool Aborted { get; private set; }

    public int DisposeCount { get; private set; }

    public int CloseCount { get; private set; }

    public Exception? ReceiveFailureAfterAuth { get; init; }

    public byte[]? AuthSibling { get; init; }

    public bool SuppressAuthReply { get; init; }

    public Func<CancellationToken, Task>? OnHeartbeat { get; set; }

    private bool _authRead;


    public IReadOnlyList<SentPacket> SentPackets => _sent;

    public IReadOnlyList<int> ReceivedOperations => _receivedOperations;

    public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var operation = BinaryPrimitives.ReadInt32BigEndian(payload.Span.Slice(8, 4));
        var version = BinaryPrimitives.ReadUInt16BigEndian(payload.Span.Slice(6, 2));
        _sent.Add(new SentPacket(operation, version, payload.Span[16..].ToArray()));
        if (operation == 7 && !SuppressAuthReply)
        {
            var auth = Frame(Utf8($"{{\"code\":{authCode}}}"), operation: 8, version: 0);
            Enqueue(AuthSibling is null ? auth : [.. auth, .. AuthSibling]);
            foreach (var item in _afterAuth)
            {
                _incoming.Writer.TryWrite(item);
            }

            _afterAuth.Clear();
            foreach (var bytes in _afterAuthFrames)
            {
                Enqueue(bytes);
            }

            _afterAuthFrames.Clear();
        }

        return operation == 2 && OnHeartbeat is { } onHeartbeat
            ? onHeartbeat(cancellationToken)
            : Task.CompletedTask;
    }

    public async Task<LiveSocketReadResult> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (_authRead && ReceiveFailureAfterAuth is { } failure)
        {
            throw failure;
        }

        var result = await _incoming.Reader.ReadAsync(cancellationToken).ConfigureAwait(true);
        if (result.Frame is not null)
        {
            _receivedOperations.Add(BinaryPrimitives.ReadInt32BigEndian(result.Frame.Span.Slice(8, 4)));
        }

        _authRead = true;
        return result;
    }

    public void Enqueue(byte[] bytes)
    {
        _incoming.Writer.TryWrite(LiveSocketReadResult.FromFrame(LiveSocketFrame.FromBytes(bytes)));
    }

    public void EnqueueClose(LiveSocketCloseKind kind)
    {
        _incoming.Writer.TryWrite(LiveSocketReadResult.Closed(kind));
    }

    public void EnqueueCloseAfterAuth(LiveSocketCloseKind kind)
    {
        _afterAuth.Add(LiveSocketReadResult.Closed(kind));
    }

    public void EnqueueAfterAuth(byte[] bytes)
    {
        _afterAuthFrames.Add(bytes);
    }

    public void Abort()
    {
        Aborted = true;
        EnqueueClose(LiveSocketCloseKind.Abnormal);
    }

    public Task CloseAsync(CancellationToken cancellationToken)
    {
        CloseCount++;
        _incoming.Writer.TryComplete();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        _incoming.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private static byte[] Frame(byte[] body, int operation, int version)
    {
        var bytes = new byte[16 + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(0, 4), bytes.Length);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4, 2), 16);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6, 2), (ushort)version);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8, 4), operation);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(12, 4), 1);
        body.CopyTo(bytes.AsSpan(16));
        return bytes;
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}

internal sealed record SentPacket(int Operation, int Version, byte[] Payload);