// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 直播数据包编解码：按大端包头长度切帧，支持 zlib/brotli 嵌套压缩与共享预算.
/// </summary>
internal static class LivePacketCodec
{
    /// <summary>
    /// 包头固定长度.
    /// </summary>
    public const int HeaderLength = 16;

    /// <summary>
    /// 编码一个直播数据包.
    /// </summary>
    /// <param name="operation">操作码.</param>
    /// <param name="version">包头协议版本.</param>
    /// <param name="body">包体.</param>
    /// <param name="sequence">序列号.</param>
    /// <returns>编码后的数据.</returns>
    public static byte[] Encode(int operation, int version, ReadOnlySpan<byte> body, int sequence = 1)
    {
        var buffer = new byte[HeaderLength + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(0, 4), buffer.Length);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(4, 2), HeaderLength);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(6, 2), (ushort)version);
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(8, 4), operation);
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(12, 4), sequence);
        body.CopyTo(buffer.AsSpan(HeaderLength));
        return buffer;
    }

    /// <summary>
    /// 解析一条 WebSocket 消息中的全部帧.
    /// </summary>
    /// <param name="buffer">完整消息字节.</param>
    /// <param name="onPacket">叶子包回调.</param>
    /// <param name="budget">解析预算，未提供时使用默认值.</param>
    /// <returns>解析结果.</returns>
    public static LiveDecodeResult Decode(ReadOnlySpan<byte> buffer, LivePacketHandler onPacket, LiveDecodeBudget? budget = null)
    {
        budget ??= new LiveDecodeBudget();
        var status = DecodeCore(buffer, onPacket, budget);
        return new LiveDecodeResult(status, budget.PacketCount, budget.ContainerPackets, budget.SkippedFrames, budget.ExpandedBytes);
    }

    private static LiveDecodeStatus DecodeCore(ReadOnlySpan<byte> buffer, LivePacketHandler onPacket, LiveDecodeBudget budget)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            if (buffer.Length - offset < HeaderLength)
            {
                return LiveDecodeStatus.Truncated;
            }

            var packetLength = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(offset, 4));
            if (packetLength < HeaderLength)
            {
                return LiveDecodeStatus.InvalidHeader;
            }

            if (packetLength > (uint)(buffer.Length - offset))
            {
                return LiveDecodeStatus.Truncated;
            }

            var headerLength = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(offset + 4, 2));
            if (headerLength < HeaderLength || headerLength > packetLength)
            {
                return LiveDecodeStatus.InvalidHeader;
            }

            var version = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(offset + 6, 2));
            var operation = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(offset + 8, 4));
            var sequence = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(offset + 12, 4));
            var payloadLength = (int)packetLength - headerLength;
            var payload = buffer.Slice(offset + headerLength, payloadLength);
            offset += (int)packetLength;

            if (!budget.TryAddPacket())
            {
                return LiveDecodeStatus.PacketCountExceeded;
            }

            switch (version)
            {
                case 0:
                case 1:
                    onPacket(operation, version, sequence, payload);
                    break;
                case 2:
                    {
                        budget.AddContainer();
                        var status = DecompressAndDecode(payload, true, onPacket, budget);
                        if (status != LiveDecodeStatus.Success)
                        {
                            return status;
                        }

                        break;
                    }

                case 3:
                    {
                        budget.AddContainer();
                        var status = DecompressAndDecode(payload, false, onPacket, budget);
                        if (status != LiveDecodeStatus.Success)
                        {
                            return status;
                        }

                        break;
                    }

                default:
                    // 未知版本跳过，不解析其内容，也不影响同消息中的其他帧.
                    budget.SkipFrame();
                    break;
            }
        }

        return LiveDecodeStatus.Success;
    }

    private static LiveDecodeStatus DecompressAndDecode(ReadOnlySpan<byte> payload, bool zlib, LivePacketHandler onPacket, LiveDecodeBudget budget)
    {
        if (!budget.EnterDecompression())
        {
            return LiveDecodeStatus.DecompressionDepthExceeded;
        }

        try
        {
            var status = ReadDecompressed(payload, zlib, budget, out var buffer, out var length);
            if (status == LiveDecodeStatus.DecompressionFailed && !zlib)
            {
                // ver=3 是兼容路径：brotli 解不开时按 zlib 再试一次，仍失败才判定连接失败.
                status = ReadDecompressed(payload, true, budget, out buffer, out length);
            }

            if (status != LiveDecodeStatus.Success)
            {
                return status;
            }

            try
            {
                return DecodeCore(buffer.AsSpan(0, length), onPacket, budget);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        finally
        {
            budget.ExitDecompression();
        }
    }

    private static LiveDecodeStatus ReadDecompressed(
        ReadOnlySpan<byte> payload,
        bool zlib,
        LiveDecodeBudget budget,
        out byte[] buffer,
        out int length)
    {
        buffer = [];
        length = 0;
        var source = ArrayPool<byte>.Shared.Rent(Math.Max(payload.Length, 1));
        try
        {
            payload.CopyTo(source);
            using var input = new MemoryStream(source, 0, payload.Length, writable: false);
            using var stream = zlib
                ? (Stream)new ZLibStream(input, CompressionMode.Decompress)
                : new BrotliStream(input, CompressionMode.Decompress);
            return ReadAll(stream, budget, out buffer, out length);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return LiveDecodeStatus.DecompressionFailed;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(source);
        }
    }

    private static LiveDecodeStatus ReadAll(Stream stream, LiveDecodeBudget budget, out byte[] buffer, out int length)
    {
        var rented = ArrayPool<byte>.Shared.Rent(16 * 1024);
        length = 0;
        try
        {
            while (true)
            {
                if (length == rented.Length)
                {
                    if (rented.Length >= budget.RemainingExpandedBytes + 1 || rented.Length >= Array.MaxLength / 2)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                        buffer = [];
                        length = 0;
                        return LiveDecodeStatus.ExpandedBudgetExceeded;
                    }

                    var larger = ArrayPool<byte>.Shared.Rent(rented.Length * 2);
                    rented.AsSpan(0, length).CopyTo(larger);
                    ArrayPool<byte>.Shared.Return(rented);
                    rented = larger;
                }

                var read = stream.Read(rented, length, rented.Length - length);
                if (read <= 0)
                {
                    break;
                }

                length += read;
                if (length > budget.RemainingExpandedBytes)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                    buffer = [];
                    length = 0;
                    return LiveDecodeStatus.ExpandedBudgetExceeded;
                }
            }

            if (!budget.AddExpandedBytes(length))
            {
                ArrayPool<byte>.Shared.Return(rented);
                buffer = [];
                length = 0;
                return LiveDecodeStatus.ExpandedBudgetExceeded;
            }

            buffer = rented;
            return LiveDecodeStatus.Success;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException)
        {
            ArrayPool<byte>.Shared.Return(rented);
            buffer = [];
            length = 0;
            return LiveDecodeStatus.DecompressionFailed;
        }
    }
}

/// <summary>
/// 直播解码状态.
/// </summary>
internal enum LiveDecodeStatus
{
    /// <summary>成功.</summary>
    Success,

    /// <summary>帧被截断或声明长度超出消息.</summary>
    Truncated,

    /// <summary>包头非法.</summary>
    InvalidHeader,

    /// <summary>解压失败.</summary>
    DecompressionFailed,

    /// <summary>展开字节超出预算.</summary>
    ExpandedBudgetExceeded,

    /// <summary>解压深度超出预算.</summary>
    DecompressionDepthExceeded,

    /// <summary>包数量超出上限.</summary>
    PacketCountExceeded,
}

/// <summary>
/// 直播解码结果.
/// </summary>
/// <param name="Status">状态.</param>
/// <param name="PacketCount">已解码包数.</param>
/// <param name="ContainerPackets">压缩容器帧数.</param>
/// <param name="SkippedFrames">跳过的未知版本帧数.</param>
/// <param name="ExpandedBytes">展开字节数.</param>
internal readonly record struct LiveDecodeResult(LiveDecodeStatus Status, int PacketCount, int ContainerPackets, int SkippedFrames, int ExpandedBytes);

/// <summary>
/// 叶子包回调.
/// </summary>
/// <param name="operation">操作码.</param>
/// <param name="version">协议版本.</param>
/// <param name="sequence">序列号.</param>
/// <param name="payload">包体.</param>
internal delegate void LivePacketHandler(int operation, int version, int sequence, ReadOnlySpan<byte> payload);