// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 单次直播消息解析的共享预算：解压深度、展开字节与包数量共用同一份记录.
/// </summary>
internal sealed class LiveDecodeBudget
{
    /// <summary>
    /// 默认单条 WebSocket 消息允许的展开字节数.
    /// </summary>
    public const int DefaultMaxExpandedBytes = 8 * 1024 * 1024;

    /// <summary>
    /// 默认单条 WebSocket 消息允许的包数量.
    /// </summary>
    public const int DefaultMaxPacketCount = 512;

    /// <summary>
    /// 默认允许的嵌套解压深度.
    /// </summary>
    public const int DefaultMaxDecompressionDepth = 4;

    private readonly int _maxExpandedBytes;
    private readonly int _maxPacketCount;
    private readonly int _maxDecompressionDepth;
    private int _depth;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveDecodeBudget"/> class.
    /// </summary>
    /// <param name="maxExpandedBytes">允许的展开字节总数.</param>
    /// <param name="maxPacketCount">允许的包总数.</param>
    /// <param name="maxDecompressionDepth">允许的嵌套解压深度.</param>
    public LiveDecodeBudget(
        int maxExpandedBytes = DefaultMaxExpandedBytes,
        int maxPacketCount = DefaultMaxPacketCount,
        int maxDecompressionDepth = DefaultMaxDecompressionDepth)
    {
        _maxExpandedBytes = maxExpandedBytes;
        _maxPacketCount = maxPacketCount;
        _maxDecompressionDepth = maxDecompressionDepth;
    }

    /// <summary>
    /// 已展开字节数.
    /// </summary>
    public int ExpandedBytes { get; private set; }

    /// <summary>
    /// 已解析包数量.
    /// </summary>
    public int PacketCount { get; private set; }

    /// <summary>
    /// 被跳过的未知版本帧数量.
    /// </summary>
    public int SkippedFrames { get; private set; }

    /// <summary>
    /// 压缩容器帧数量（不直接产出事件）.
    /// </summary>
    public int ContainerPackets { get; private set; }

    /// <summary>
    /// 当前解压深度.
    /// </summary>
    public int Depth => _depth;

    /// <summary>
    /// 还可展开的字节数.
    /// </summary>
    public int RemainingExpandedBytes => _maxExpandedBytes - ExpandedBytes;

    /// <summary>
    /// 登记一个包；超过数量上限时返回 false.
    /// </summary>
    /// <returns>是否仍在预算内.</returns>
    public bool TryAddPacket()
    {
        if (PacketCount >= _maxPacketCount)
        {
            return false;
        }

        PacketCount++;
        return true;
    }

    /// <summary>
    /// 登记跳过的帧.
    /// </summary>
    public void SkipFrame() => SkippedFrames++;

    /// <summary>
    /// 登记压缩容器帧.
    /// </summary>
    public void AddContainer() => ContainerPackets++;

    /// <summary>
    /// 进入一层解压；超过深度上限时返回 false.
    /// </summary>
    /// <returns>是否仍在预算内.</returns>
    public bool EnterDecompression()
    {
        if (_depth >= _maxDecompressionDepth)
        {
            return false;
        }

        _depth++;
        return true;
    }

    /// <summary>
    /// 退出一层解压.
    /// </summary>
    public void ExitDecompression() => _depth--;

    /// <summary>
    /// 在成功解压后登记展开字节.
    /// </summary>
    /// <param name="bytes">展开字节数.</param>
    /// <returns>是否仍在预算内.</returns>
    public bool AddExpandedBytes(int bytes)
    {
        if (bytes < 0 || bytes > RemainingExpandedBytes)
        {
            return false;
        }

        ExpandedBytes += bytes;
        return true;
    }
}
