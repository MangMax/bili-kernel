// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media.Core;
using Richasy.BiliKernel.Services.Media.Core.Models;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 直播线路保留接口的 <c>format_name</c>：容器排序不再靠地址猜。
/// </summary>
public sealed class LiveLineFormatNameTests
{
    [Fact]
    public void KeepsFormatNamePerLine()
    {
        var information = LiveAdapter.ToLiveMediaInformation(CreateResponse("fmp4"));

        var line = Assert.Single(information.Lines);
        Assert.Equal("fmp4", line.FormatName);
        Assert.Equal("avc", line.Name);
        Assert.Equal(10000, line.Quality);
    }

    [Fact]
    public void LeavesFormatNameNullWhenServerOmitsIt()
    {
        var information = LiveAdapter.ToLiveMediaInformation(CreateResponse(null));

        Assert.Null(Assert.Single(information.Lines).FormatName);
    }

    [Fact]
    public void LeavesFormatNameNullWhenServerSendsBlank()
    {
        var information = LiveAdapter.ToLiveMediaInformation(CreateResponse("   "));

        Assert.Null(Assert.Single(information.Lines).FormatName);
    }

    [Fact]
    public void ExistingFourArgumentConstructorStillWorks()
    {
        // 视频/PGC 的公开签名不变；直播线路的既有构造方式仍然可用。
        var line = new LiveLineInformation("avc", 10000, [10000], []);

        Assert.Null(line.FormatName);
        Assert.Equal("avc", line.Name);
    }

    [Fact]
    public void ExposesOptionalFormatName()
    {
        var line = new LiveLineInformation("avc", 10000, [10000], [], "ts");

        Assert.Equal("ts", line.FormatName);
    }

    private static LiveAppPlayResponse CreateResponse(string? formatName)
        => new()
        {
            RoomId = 1234,
            PlayUrlInfo = new LiveAppPlayUrlInfo
            {
                PlayUrl = new LiveAppPlayData
                {
                    Descriptions =
                    [
                        new LiveAppQualityDescription { Quality = 10000, Description = "原画" },
                    ],
                    StreamList =
                    [
                        new LiveAppPlayStream
                        {
                            ProtocolName = "http_hls",
                            FormatList =
                            [
                                new LiveAppPlayFormat
                                {
                                    FormatName = formatName!,
                                    CodecList =
                                    [
                                        new LiveAppPlayCodec
                                        {
                                            CodecName = "avc",
                                            CurrentQuality = 10000,
                                            AcceptQualities = [10000],
                                            BaseUrl = "/live/stream.m3u8",
                                            Urls =
                                            [
                                                new LiveAppPlayUrl { Host = "https://cdn.example.com", Extra = "?token=1" },
                                            ],
                                        },
                                    ],
                                },
                            ],
                        },
                    ],
                },
            },
        };
}
