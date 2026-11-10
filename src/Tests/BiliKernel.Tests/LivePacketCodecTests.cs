// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Services.Media.Core.Live;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 直播数据包编解码测试：帧切分、压缩嵌套、预算与兼容解压。
/// </summary>
public sealed class LivePacketCodecTests
{
    [Fact]
    public void MultipleFramesInOneMessageAreAllDecoded()
    {
        var buffer = Concat(
            Frame(Utf8("{\"cmd\":\"A\"}")),
            Frame(Utf8("{\"cmd\":\"B\"}")),
            Frame(Utf8("{\"cmd\":\"C\"}")));

        var packets = Decode(buffer, out var result);

        Assert.Equal(LiveDecodeStatus.Success, result.Status);
        Assert.Equal(3, result.PacketCount);
        Assert.Equal([Cmd("A"), Cmd("B"), Cmd("C")], packets.Select(p => p.Text));
        Assert.All(packets, p => Assert.Equal(5, p.Operation));
    }

    [Fact]
    public void TruncatedFrameFailsWithoutEmittingFragment()
    {
        var valid = Frame(Utf8("{\"cmd\":\"A\"}"));
        var truncated = Frame(Utf8("{\"cmd\":\"B\"}"), declaredLength: 100);
        var buffer = Concat(valid, truncated);

        var packets = Decode(buffer, out var result);

        Assert.Equal(LiveDecodeStatus.Truncated, result.Status);
        // 前一帧合法，后一帧声明长度超出剩余字节，不能输出残片。
        Assert.Single(packets);
    }

    [Fact]
    public void UnknownVersionIsSkippedAndFollowingFrameIsDecoded()
    {
        var buffer = Concat(
            Frame(Utf8("{\"cmd\":\"unknown\"}"), version: 9),
            Frame(Utf8("{\"cmd\":\"A\"}")));

        var packets = Decode(buffer, out var result);

        Assert.Equal(LiveDecodeStatus.Success, result.Status);
        Assert.Single(packets);
        Assert.Equal(Cmd("A"), packets[0].Text);
        Assert.Equal(1, result.SkippedFrames);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(0x8004)]
    public void ExtensionHeaderIsReadAsUnsignedLength(int headerLength)
    {
        var body = Utf8("{\"cmd\":\"A\"}");
        var buffer = Frame(body, headerLength: headerLength);

        var packets = Decode(buffer, out var result);

        Assert.Equal(LiveDecodeStatus.Success, result.Status);
        Assert.Single(packets);
        Assert.Equal(Cmd("A"), packets[0].Text);
    }

    [Fact]
    public void HeaderLengthBelowSixteenFails()
    {
        var buffer = Frame(Utf8("{\"cmd\":\"A\"}"), headerLength: 12);

        var packets = Decode(buffer, out var result);

        Assert.Equal(LiveDecodeStatus.InvalidHeader, result.Status);
        Assert.Empty(packets);
    }

    [Fact]
    public void NestedCompressedFramesAreDecoded()
    {
        var inner = Concat(
            Frame(Utf8("{\"cmd\":\"A\"}")),
            Frame(Utf8("{\"cmd\":\"B\"}")));
        var compressedInner = Frame(Zlib(inner), version: 2);
        var outer = Frame(Zlib(compressedInner), version: 2);

        var packets = Decode(outer, out var result);

        Assert.Equal(LiveDecodeStatus.Success, result.Status);
        Assert.Equal(4, result.PacketCount);
        Assert.Equal([Cmd("A"), Cmd("B")], packets.Select(p => p.Text));
    }

    [Fact]
    public void CompressedFrameAndPlainSiblingInSameMessageAreDecoded()
    {
        var compressed = Frame(Zlib(Frame(Utf8("{\"cmd\":\"A\"}"))), version: 2);
        var plain = Frame(Utf8("{\"cmd\":\"B\"}"));
        var buffer = Concat(compressed, plain);

        var packets = Decode(buffer, out var result);

        Assert.Equal(LiveDecodeStatus.Success, result.Status);
        Assert.Equal([Cmd("A"), Cmd("B")], packets.Select(p => p.Text));
    }

    [Fact]
    public void BrotliFrameIsDecodedAsCompatibility()
    {
        var outer = Frame(Brotli(Concat(
            Frame(Utf8("{\"cmd\":\"A\"}")),
            Frame(Utf8("{\"cmd\":\"B\"}")))), version: 3);

        var packets = Decode(outer, out var result);

        Assert.Equal(LiveDecodeStatus.Success, result.Status);
        Assert.Equal([Cmd("A"), Cmd("B")], packets.Select(p => p.Text));
    }

    [Fact]
    public void VersionThreeRejectsZlibPayloadInsteadOfGuessingCompression()
    {
        var outer = Frame(Zlib(Frame(Utf8("{}"))), version: 3);
        var packets = Decode(outer, out var result);
        Assert.Equal(LiveDecodeStatus.DecompressionFailed, result.Status);
        Assert.Empty(packets);
    }

    [Fact]
    public void DecompressionOverExpandedBudgetFails()
    {
        var inner = new byte[4096];
        var outer = Frame(Zlib(inner), version: 2);

        var packets = Decode(outer, out var result, new LiveDecodeBudget(maxExpandedBytes: 1024));

        Assert.Equal(LiveDecodeStatus.ExpandedBudgetExceeded, result.Status);
        Assert.Empty(packets);
    }

    [Fact]
    public void DecompressionDepthOverBudgetFails()
    {
        var buffer = Frame(Utf8("{\"cmd\":\"A\"}"));
        for (var i = 0; i < 6; i++)
        {
            buffer = Frame(Zlib(buffer), version: 2);
        }

        var packets = Decode(buffer, out var result);

        Assert.Equal(LiveDecodeStatus.DecompressionDepthExceeded, result.Status);
        Assert.Empty(packets);
    }

    [Fact]
    public void PacketCountOverDefaultLimitFails()
    {
        var frames = Enumerable.Range(0, 513).Select(_ => Frame(Utf8("{}"))).ToArray();
        var buffer = Concat(frames);

        var packets = Decode(buffer, out var result);

        Assert.Equal(LiveDecodeStatus.PacketCountExceeded, result.Status);
        Assert.Equal(512, packets.Count);
    }

    [Fact]
    public void EncodedPacketUsesBigEndianHeaderAndHeaderVersion()
    {
        var bytes = LivePacketCodec.Encode(7, 1, Utf8("{}"), sequence: 3);

        Assert.Equal(bytes.Length, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(0, 4)));
        Assert.Equal(16, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4, 2)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(6, 2)));
        Assert.Equal(7, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(8, 4)));
        Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(12, 4)));
        Assert.Equal("{}", Encoding.UTF8.GetString(bytes.AsSpan(16)));
    }

    private static List<(int Operation, int Version, string Text)> Decode(byte[] buffer, out LiveDecodeResult result, LiveDecodeBudget? budget = null)
    {
        var packets = new List<(int, int, string)>();
        result = LivePacketCodec.Decode(
            buffer,
            (operation, version, _, payload) => packets.Add((operation, version, Encoding.UTF8.GetString(payload))),
            budget);
        return packets;
    }

    private static byte[] Frame(byte[] body, int operation = 5, int version = 0, int headerLength = 16, int? declaredLength = null)
    {
        var length = declaredLength ?? headerLength + body.Length;
        var bytes = new byte[headerLength + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(0, 4), length);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4, 2), (ushort)headerLength);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6, 2), (ushort)version);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8, 4), operation);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(12, 4), 1);
        if (headerLength > 16)
        {
            bytes.AsSpan(16, headerLength - 16).Fill(0x5A);
        }

        body.CopyTo(bytes.AsSpan(headerLength));
        return bytes;
    }

    private static byte[] Zlib(byte[] data)
    {
        using var output = new MemoryStream();
        using (var stream = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            stream.Write(data);
        }

        return output.ToArray();
    }

    private static byte[] Brotli(byte[] data)
    {
        using var output = new MemoryStream();
        using (var stream = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            stream.Write(data);
        }

        return output.ToArray();
    }

    private static string Cmd(string name) => $"{{\"cmd\":\"{name}\"}}";

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static byte[] Concat(params byte[][] items)
    {
        var bytes = new byte[items.Sum(item => item.Length)];
        var offset = 0;
        foreach (var item in items)
        {
            item.CopyTo(bytes, offset);
            offset += item.Length;
        }

        return bytes;
    }
}
