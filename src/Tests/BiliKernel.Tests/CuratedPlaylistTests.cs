// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Services.Media;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 首页精选推荐请求形状测试。
/// </summary>
public sealed class CuratedPlaylistTests
{
    private const string NavResponse = @"{""code"":0,""data"":{""wbi_img"":{""img_url"":""https://i0.hdslb.com/bfs/wbi/7cd084941338484aae1ad9425b84077c.png"",""sub_url"":""https://i0.hdslb.com/bfs/wbi/4932caff0ff746eab6f01bf08b70ac45.png""}}}";
    private const string SpiResponse = @"{""code"":0,""data"":{""b_3"":""BUV3"",""b_4"":""BUV4""}}";
    private const string PlaylistResponse = @"{""code"":0,""data"":{""item"":[{""id"":1,""bvid"":""BV1xx411c7mD"",""cid"":9,""goto"":""av"",""uri"":""bilibili://video/1"",""pic"":""https://i0.hdslb.com/a.jpg"",""title"":""t"",""duration"":10,""pubdate"":1600000000,""owner"":{""mid"":1,""name"":""up""},""stat"":{""view"":1,""like"":1,""danmaku"":1}}]}}";

    [Fact]
    public async Task CuratedPlaylistUsesWbiVariant()
    {
        var handler = new RecordingHttpMessageHandler()
            .With("/x/web-interface/nav", NavResponse)
            .With("/x/frontend/finger/spi", SpiResponse)
            .With("/x/web-interface/wbi/index/top/feed/rcmd", PlaylistResponse);
        var cookies = new FakeCookiesResolver();
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        var authenticator = new BiliAuthenticator(cookies, new FakeTokenResolver(), buvid, http);
        var service = new VideoDiscoveryService(http, authenticator, new FakeTokenResolver());

        _ = await service.GetCuratedPlaylistAsync(CancellationToken.None);

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("feed/rcmd", StringComparison.Ordinal));
        Assert.Equal("/x/web-interface/wbi/index/top/feed/rcmd", request.Path);
        Assert.True(request.Query.ContainsKey("w_rid"));
        Assert.Equal("1", request.Query["fresh_idx"]);
        Assert.Equal("10", request.Query["ps"]);
        Assert.Equal("1", request.Query["plat"]);
    }
}