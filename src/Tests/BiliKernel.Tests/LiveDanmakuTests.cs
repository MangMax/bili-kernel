// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Services.Media.Core.Live;
using RichasyKernel;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 直播弹幕端点解析与请求形状测试（不联网）.
/// </summary>
public sealed class LiveDanmakuTests
{
    private const string NavResponse = @"{""code"":0,""data"":{""wbi_img"":{""img_url"":""https://i0.hdslb.com/bfs/wbi/7cd084941338484aae1ad9425b84077c.png"",""sub_url"":""https://i0.hdslb.com/bfs/wbi/4932caff0ff746eab6f01bf08b70ac45.png""}}}";
    private const string RoomInitResponse = @"{""code"":0,""data"":{""room_id"":1234}}";
    private const string DanmakuResponse = @"{""code"":0,""data"":{""token"":""chat-token"",""host_list"":[{""host"":""chat.example.test"",""wss_port"":2245},{""host"":""chat2.example.test"",""wss_port"":443}]}}";

    [Fact]
    public async Task EndpointNormalizesShortRoomIdAndUsesWebCredentials()
    {
        var (handler, client, tokens) = Create(DanmakuResponse);
        tokens.Token = new BiliToken { AccessToken = "app-token", UserId = 42 };

        var endpoint = await client.ResolveEndpointAsync("123", CancellationToken.None).ConfigureAwait(true);

        Assert.Equal("1234", endpoint.RoomId);
        Assert.Equal("chat-token", endpoint.Token);
        Assert.Equal(42, endpoint.UserId);
        Assert.Equal("B3", endpoint.Buvid);
        Assert.Equal(2, endpoint.Hosts.Count);
        Assert.Equal(new LiveHost("chat.example.test", 2245), endpoint.Hosts[0]);
        Assert.Equal(new LiveHost("chat2.example.test", 443), endpoint.Hosts[1]);

        var roomInit = Assert.Single(handler.Requests, r => r.Path.EndsWith("/room/v1/Room/room_init", StringComparison.Ordinal));
        Assert.Equal("123", roomInit.Query["id"]);

        var danmaku = Assert.Single(handler.Requests, r => r.Path.EndsWith("/xlive/web-room/v1/index/getDanmuInfo", StringComparison.Ordinal));
        Assert.Equal("1234", danmaku.Query["id"]);
        Assert.Equal("0", danmaku.Query["type"]);
        Assert.Equal("444.8", danmaku.Query["web_location"]);
        Assert.False(danmaku.Query.ContainsKey("access_key"));
        Assert.True(danmaku.Query.ContainsKey("w_rid"));
        Assert.True(danmaku.Query.ContainsKey("wts"));
        Assert.Contains("SESSDATA=session", danmaku.Cookie);
        Assert.Contains("buvid3=B3", danmaku.Cookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RoomInitFailureFallsBackToInputRoomId()
    {
        var (handler, client, _) = Create(DanmakuResponse);
        handler.With("/room/v1/Room/room_init", @"{""code"":-404,""message"":""no such room""}");

        var endpoint = await client.ResolveEndpointAsync("123", CancellationToken.None).ConfigureAwait(true);

        Assert.Equal("123", endpoint.RoomId);
        Assert.Equal("123", Assert.Single(handler.Requests, r => r.Path.Contains("getDanmuInfo", StringComparison.Ordinal)).Query["id"]);
    }

    [Fact]
    public async Task EmptyHostListFailsExplicitly()
    {
        var (_, client, _) = Create(@"{""code"":0,""data"":{""token"":""chat-token"",""host_list"":[]}}");

        var error = await Assert.ThrowsAsync<KernelException>(
            () => client.ResolveEndpointAsync("123", CancellationToken.None));

        Assert.Equal("无法获取直播弹幕服务器地址", error.Message);
    }

    [Fact]
    public async Task DanmakuRejectionPropagates()
    {
        var (_, client, _) = Create(@"{""code"":-101,""message"":""chat denied""}");

        var error = await Assert.ThrowsAsync<KernelException>(
            () => client.ResolveEndpointAsync("123", CancellationToken.None));

        Assert.Contains("chat denied", error.Message);
    }

    private static (RecordingHttpMessageHandler Handler, LiveDanmakuApiClient Client, FakeTokenResolver Tokens) Create(string danmakuResponse)
    {
        var handler = new RecordingHttpMessageHandler()
            .With("/x/web-interface/nav", NavResponse)
            .With("/room/v1/Room/room_init", RoomInitResponse)
            .With("/xlive/web-room/v1/index/getDanmuInfo", danmakuResponse);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session" });
        var tokens = new FakeTokenResolver { Token = new BiliToken { AccessToken = "app-token" } };
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        var authenticator = new BiliAuthenticator(cookies, tokens, buvid, http);
        return (handler, new LiveDanmakuApiClient(http, authenticator, tokens, buvid), tokens);
    }
}