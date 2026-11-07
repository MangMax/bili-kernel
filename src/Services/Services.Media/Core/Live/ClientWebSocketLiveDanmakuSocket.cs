// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Bili.Media;
using System.Buffers;
using System.Net.WebSockets;

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 基于 <see cref="ClientWebSocket"/> 的直播弹幕 socket：累积分片到 EndOfMessage，消息缓冲使用 ArrayPool.
/// </summary>
internal sealed class ClientWebSocketLiveDanmakuSocket : ILiveDanmakuSocket
{
    private const int InitialBufferSize = 16 * 1024;
    private const int MaxMessageBytes = 16 * 1024 * 1024;
    private const string WebUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36 Edg/116.0.1938.69";

    private readonly ClientWebSocket _socket = new();

    /// <summary>
    /// 建立连接.
    /// </summary>
    /// <param name="uri">地址.</param>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>连接任务.</returns>
    public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        _socket.Options.SetRequestHeader("User-Agent", WebUserAgent);
        await _socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        => await _socket.SendAsync(payload, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<LiveSocketReadResult> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
        var length = 0;
        try
        {
            while (true)
            {
                if (length == buffer.Length)
                {
                    if (buffer.Length >= MaxMessageBytes)
                    {
                        throw new LiveDanmakuProtocolException("WebSocket 消息超过大小上限");
                    }

                    var larger = ArrayPool<byte>.Shared.Rent(Math.Min(buffer.Length * 2, MaxMessageBytes));
                    buffer.AsSpan(0, length).CopyTo(larger);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = larger;
                }

                var result = await _socket.ReceiveAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    var kind = _socket.CloseStatus == WebSocketCloseStatus.NormalClosure
                        ? LiveSocketCloseKind.Normal
                        : LiveSocketCloseKind.Abnormal;
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = [];
                    return LiveSocketReadResult.Closed(kind);
                }

                length += result.Count;
                if (length > MaxMessageBytes)
                {
                    throw new LiveDanmakuProtocolException("WebSocket 消息超过大小上限");
                }

                if (result.EndOfMessage)
                {
                    var frame = LiveSocketFrame.FromPooled(buffer, length);
                    buffer = [];
                    return LiveSocketReadResult.FromFrame(frame);
                }
            }
        }
        finally
        {
            if (buffer.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <inheritdoc/>
    public void Abort() => _socket.Abort();

    /// <inheritdoc/>
    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
            // 对端已断开时关闭会失败，不影响清理.
        }
        finally
        {
            _socket.Abort();
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _socket.Abort();
        _socket.Dispose();
        return ValueTask.CompletedTask;
    }
}
