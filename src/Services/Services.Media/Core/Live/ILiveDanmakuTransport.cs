// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 直播弹幕传输接缝：解析端点与建立 socket，便于用内存替身测试.
/// </summary>
internal interface ILiveDanmakuTransport
{
    /// <summary>
    /// 解析真实房间号、token、buvid 与主机列表.
    /// </summary>
    /// <param name="roomId">房间标识.</param>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>端点信息.</returns>
    Task<LiveDanmakuEndpoint> ResolveEndpointAsync(string roomId, CancellationToken cancellationToken);

    /// <summary>
    /// 建立 WebSocket 连接.
    /// </summary>
    /// <param name="uri">地址.</param>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>socket.</returns>
    Task<ILiveDanmakuSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken);
}

/// <summary>
/// 直播弹幕 socket 接缝.
/// </summary>
internal interface ILiveDanmakuSocket : IAsyncDisposable
{
    /// <summary>
    /// 发送一条完整消息.
    /// </summary>
    /// <param name="payload">数据.</param>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>发送任务.</returns>
    Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);

    /// <summary>
    /// 接收一条完整 WebSocket 消息.
    /// </summary>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>接收结果.</returns>
    Task<LiveSocketReadResult> ReceiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 强制中止连接，用于心跳失效.
    /// </summary>
    void Abort();

    /// <summary>
    /// 正常关闭.
    /// </summary>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>关闭任务.</returns>
    Task CloseAsync(CancellationToken cancellationToken);
}

/// <summary>
/// socket 关闭类型.
/// </summary>
internal enum LiveSocketCloseKind
{
    /// <summary>未关闭.</summary>
    None,

    /// <summary>服务端正常关闭.</summary>
    Normal,

    /// <summary>异常断开.</summary>
    Abnormal,
}

/// <summary>
/// socket 读取结果.
/// </summary>
/// <param name="Frame">完整消息；关闭时为 null.</param>
/// <param name="CloseKind">关闭类型.</param>
internal readonly record struct LiveSocketReadResult(LiveSocketFrame? Frame, LiveSocketCloseKind CloseKind)
{
    /// <summary>
    /// 由完整消息构造结果.
    /// </summary>
    /// <param name="frame">完整消息.</param>
    /// <returns>读取结果.</returns>
    public static LiveSocketReadResult FromFrame(LiveSocketFrame frame) => new(frame, LiveSocketCloseKind.None);

    /// <summary>
    /// 由关闭类型构造结果.
    /// </summary>
    /// <param name="kind">关闭类型.</param>
    /// <returns>读取结果.</returns>
    public static LiveSocketReadResult Closed(LiveSocketCloseKind kind) => new(null, kind);
}

/// <summary>
/// 一条完整 WebSocket 消息，可归还 ArrayPool.
/// </summary>
internal sealed class LiveSocketFrame : IDisposable
{
    private byte[]? _buffer;
    private readonly int _length;
    private readonly bool _pooled;

    private LiveSocketFrame(byte[] buffer, int length, bool pooled)
    {
        _buffer = buffer;
        _length = length;
        _pooled = pooled;
    }

    /// <summary>
    /// 消息内容.
    /// </summary>
    public ReadOnlySpan<byte> Span => (_buffer ?? throw new ObjectDisposedException(nameof(LiveSocketFrame))).AsSpan(0, _length);

    /// <summary>
    /// 从普通数组构造.
    /// </summary>
    /// <param name="bytes">字节.</param>
    /// <returns>消息.</returns>
    public static LiveSocketFrame FromBytes(byte[] bytes) => new(bytes, bytes.Length, false);

    /// <summary>
    /// 从池化缓冲区构造，释放时归还.
    /// </summary>
    /// <param name="buffer">缓冲区.</param>
    /// <param name="length">有效长度.</param>
    /// <returns>消息.</returns>
    public static LiveSocketFrame FromPooled(byte[] buffer, int length) => new(buffer, length, true);

    /// <inheritdoc/>
    public void Dispose()
    {
        var buffer = Interlocked.Exchange(ref _buffer, null);
        if (buffer is not null && _pooled)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
