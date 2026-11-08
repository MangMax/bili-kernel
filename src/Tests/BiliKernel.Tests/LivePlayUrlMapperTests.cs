// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Services.Media.Core.Live;
using Xunit;

namespace BiliKernel.Tests;

public class LivePlayUrlMapperTests
{
    [Fact]
    public void OldDurlMapsToAbsoluteFlvLine()
    {
        var line = LivePlayUrlMapper.CreateOldLine(["https://cn.example.com/live/a.flv?expires=1&sign=x"], 250);

        Assert.NotNull(line);
        Assert.Equal("flv", line.FormatName);
        Assert.Equal(250, line.Quality);
        var url = Assert.Single(line.Urls);
        Assert.Equal("http_stream", url.Protocol);
        Assert.Equal("https://cn.example.com", url.Host);
        Assert.Equal("/live/a.flv", url.Route);
        Assert.Equal("?expires=1&sign=x", url.Query);
        Assert.Equal("https://cn.example.com/live/a.flv?expires=1&sign=x", url.ToString());
    }

    [Fact]
    public void OldDurlSkipsInvalidUrls()
    {
        var line = LivePlayUrlMapper.CreateOldLine([string.Empty, "not-a-url", "https://ok.example.com/live/b.flv"], 250);

        Assert.NotNull(line);
        Assert.Single(line.Urls);
        Assert.Null(LivePlayUrlMapper.CreateOldLine([string.Empty], 250));
    }
}