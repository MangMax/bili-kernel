// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models.Media;

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 线程安全的直播弹幕计数.
/// </summary>
internal sealed class LiveStatisticsCounters
{
    private long _frames;
    private long _decoded;
    private long _parsed;
    private long _unknown;
    private long _malformed;
    private long _ignored;
    private long _skipped;
    private long _reconnects;

    public void AddFrame() => Interlocked.Increment(ref _frames);

    public void AddDecoded(long count) => Interlocked.Add(ref _decoded, count);

    public void AddParsed() => Interlocked.Increment(ref _parsed);

    public void AddUnknown() => Interlocked.Increment(ref _unknown);

    public void AddMalformed() => Interlocked.Increment(ref _malformed);

    public void AddIgnored() => Interlocked.Increment(ref _ignored);

    public void AddSkipped() => Interlocked.Increment(ref _skipped);

    public void AddSkipped(long count) => Interlocked.Add(ref _skipped, count);

    public void AddReconnect() => Interlocked.Increment(ref _reconnects);

    public LiveDanmakuStatistics Snapshot()
        => new()
        {
            ReceivedFrames = Interlocked.Read(ref _frames),
            DecodedPackets = Interlocked.Read(ref _decoded),
            ParsedMessages = Interlocked.Read(ref _parsed),
            UnknownCommands = Interlocked.Read(ref _unknown),
            MalformedMessages = Interlocked.Read(ref _malformed),
            IgnoredMessages = Interlocked.Read(ref _ignored),
            SkippedPackets = Interlocked.Read(ref _skipped),
            Reconnects = Interlocked.Read(ref _reconnects),
        };
}
