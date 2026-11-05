// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Services.Media;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 直播弹幕请求形状测试。
/// </summary>
public sealed class LiveDanmakuTests
{
    private const string NavResponse = @"{""code"":0,""data"":{""wbi_img"":{""img_url"":""https://i0.hdslb.com/bfs/wbi/7cd084941338484aae1ad9425b84077c.png"",""sub_url"":""https://i0.hdslb.com/bfs/wbi/4932caff0ff746eab6f01bf08b70ac45.png""}}}";
    private const string SpiResponse = @"{""code"":0,""data"":{""b_3"":""BUV3"",""b_4"":""BUV4""}}";
    private const string DanmakuResponse = @"{""code"":0,""data"":{""token"":""token-value"",""host_list"":[]}}";

    [Fact]
    public async Task DanmakuInfoRequestIsSignedAndTaggedWithWebLocation()
    {
        var handler = new RecordingHttpMessageHandler()
            .With("/x/web-interface/nav", NavResponse)
            .With("/x/frontend/finger/spi", SpiResponse)
            .With("/xlive/web-room/v1/index/getDanmuInfo", DanmakuResponse);
        var cookies = new FakeCookiesResolver();
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session" });
        var tokens = new FakeTokenResolver { Token = new Richasy.BiliKernel.Models.Authorization.BiliToken { AccessToken = "app-token" } };
        var authenticator = new BiliAuthenticator(cookies, tokens, buvid, http);
        var service = new PlayerService(http, authenticator, new FakeTokenResolver());

        // 没有可用的弹幕服务器地址时会失败，但弹幕信息请求已经发出。
        var error = await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => service.EnterLiveRoomAsync("1234", CancellationToken.None));
        Assert.Equal("无法获取直播弹幕服务器地址", error.Message);

        var danmaku = Assert.Single(handler.Requests, p => p.Path.Contains("getDanmuInfo", StringComparison.Ordinal));
        Assert.False(danmaku.Query.ContainsKey("access_key"));
        Assert.Contains("SESSDATA=session", danmaku.Cookie);
        Assert.Equal("1234", danmaku.Query["id"]);
        Assert.Equal("0", danmaku.Query["type"]);
        Assert.Equal("444.8", danmaku.Query["web_location"]);
        Assert.True(danmaku.Query.ContainsKey("w_rid"));
        Assert.True(danmaku.Query.ContainsKey("wts"));
        Assert.Contains("buvid3=B3", danmaku.Cookie, StringComparison.Ordinal);
    }
}