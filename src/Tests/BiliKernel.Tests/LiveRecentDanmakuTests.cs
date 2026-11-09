// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 直播最近弹幕补屏：映射 admin/room 两组消息，缺字段时保守降级。
/// </summary>
public sealed class LiveRecentDanmakuTests
{
    private const string HistoryPath = "/xlive/web-room/v1/dM/gethistory";
    private const string History = """
        {"code":0,"data":{"room":[
            {"text":"room one","uid":1001,"nickname":"viewer","isadmin":0,"guard_level":0,"user_level":12,"timeline":"2026-11-08 12:00:00","color":16777215},
            {"text":"room two","uid":1002,"nickname":"viewer2","isadmin":0,"guard_level":3,"user_level":30,"timeline":"2026-11-08 12:00:01","color":16711680}
        ],"admin":[
            {"text":"admin one","uid":2001,"nickname":"host","isadmin":1,"guard_level":1,"user_level":60,"timeline":"2026-11-08 11:59:59","color":16777215}
        ]}}
        """;

    [Fact]
    public async Task ReturnsAdminThenRoomMessagesMappedToDanmaku()
    {
        var (handler, service) = Create(History);

        var messages = await service.GetRecentLiveDanmakuAsync(new MediaIdentifier("1234", null, null));

        Assert.Equal(3, messages.Count);
        Assert.Equal(["admin one", "room one", "room two"], messages.Select(p => p.Text));
        Assert.Equal(2001, messages[0].UserId);
        Assert.True(messages[0].IsAdmin);
        Assert.Equal(1, messages[0].GuardLevel);
        Assert.Equal("viewer2", messages[2].UserName);
        Assert.Equal(3, messages[2].GuardLevel);
        // 与实时弹幕解析器保持一致：等级统一加 UL 前缀。
        Assert.Equal("UL30", messages[2].UserLevel);

        var request = Assert.Single(handler.Requests, r => r.Path == HistoryPath);
        Assert.Equal("1234", request.Query["roomid"]);
    }

    [Fact]
    public async Task ReturnsEmptyWhenDataIsMissing()
    {
        var (_, service) = Create("""{"code":0,"data":null}""");

        var messages = await service.GetRecentLiveDanmakuAsync(new MediaIdentifier("1234", null, null));

        Assert.Empty(messages);
    }

    [Fact]
    public async Task SkipsEntriesWithoutText()
    {
        var (_, service) = Create("""{"code":0,"data":{"room":[{"text":"","uid":1},{"uid":2,"nickname":"x"}],"admin":[{"text":"ok","uid":3,"nickname":"host"}]}}""");

        var messages = await service.GetRecentLiveDanmakuAsync(new MediaIdentifier("1234", null, null));

        Assert.Single(messages);
        Assert.Equal("ok", messages[0].Text);
    }

    [Fact]
    public async Task NestedUserStructureCarriesNameMedalAndEmotes()
    {
        var (_, service) = Create("""{"code":0,"data":{"room":[{"text":"hi [doge]","user":{"uid":77,"base":{"name":"Nested","face":"https://example.com/f.jpg"},"medal":{"name":"Fan","level":9,"is_light":1,"v2_medal_color_start":"#FF6699"},"guard":{"level":3}},"emots":{"[doge]":{"url":"https://example.com/doge.png","width":20,"height":20}},"user_level":21,"timeline":"2026-11-08 12:00:00","color":16777215}]}}""");

        var messages = await service.GetRecentLiveDanmakuAsync(new MediaIdentifier("1234", null, null));

        var message = Assert.Single(messages);
        Assert.Equal("Nested", message.UserName);
        Assert.Equal(77, message.UserId);
        Assert.Equal("https://example.com/f.jpg", message.UserFace);
        Assert.Equal("Fan", message.MedalName);
        Assert.Equal("9", message.MedalLevel);
        Assert.Equal("#FF6699", message.MedalColor);
        Assert.Equal(3, message.GuardLevel);
        Assert.Equal(21, message.UserLevelNumber);
        Assert.NotNull(message.Emotes);
        Assert.Equal("https://example.com/doge.png", message.Emotes["doge"].Url);
    }

    [Fact]
    public async Task MissingNicknameFallsBackToUnameThenUid()
    {
        var (_, service) = Create("""{"code":0,"data":{"room":[{"text":"has uname","uid":11,"uname":"U11","isadmin":0,"guard_level":0,"user_level":5,"timeline":"2026-11-08 12:00:00","color":16777215},{"text":"uid only","uid":22,"isadmin":0,"guard_level":0,"user_level":5,"timeline":"2026-11-08 12:00:01","color":16777215}]}}""");

        var messages = await service.GetRecentLiveDanmakuAsync(new MediaIdentifier("1234", null, null));

        Assert.Equal(2, messages.Count);
        Assert.Equal("U11", messages[0].UserName);
        Assert.Equal("用户22", messages[1].UserName);
    }

    [Fact]
    public async Task UserLevelAsArrayDoesNotFailBackfill()
    {
        var (_, service) = Create("""{"code":0,"data":{"room":[{"text":"array level","uid":1001,"nickname":"viewer","isadmin":0,"guard_level":0,"user_level":[12],"timeline":"2026-11-08 12:00:00","color":16777215}]}}""");

        var messages = await service.GetRecentLiveDanmakuAsync(new MediaIdentifier("1234", null, null));

        var message = Assert.Single(messages);
        Assert.Equal("array level", message.Text);
        Assert.Equal("UL12", message.UserLevel);
    }

    [Fact]
    public async Task RejectionPropagatesAsKernelException()
    {
        var (_, service) = Create("""{"code":-400,"message":"room not found"}""");

        var error = await Assert.ThrowsAsync<RichasyKernel.KernelException>(
            () => service.GetRecentLiveDanmakuAsync(new MediaIdentifier("1234", null, null)));
        Assert.Contains("room not found", error.Message);
    }

    [Fact]
    public async Task EmptyRoomIdRejectsWithoutNetwork()
    {
        var (handler, service) = Create(History);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.GetRecentLiveDanmakuAsync(new MediaIdentifier(string.Empty, null, null)));
        Assert.Empty(handler.Requests);
    }

    private static (RecordingHttpMessageHandler, PlayerService) Create(string json)
    {
        var handler = new RecordingHttpMessageHandler()
            .With("/x/web-interface/nav", """{"code":0,"data":{"wbi_img":{"img_url":"https://example.com/7cd084941338484aae1ad9425b84077c.png","sub_url":"https://example.com/4932caff0ff746eab6f01bf08b70ac45.png"}}}""")
            .With(HistoryPath, json);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session" });
        var tokens = new FakeTokenResolver { Token = new() { AccessToken = "app-token" } };
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        return (handler, new PlayerService(http, new BiliAuthenticator(cookies, tokens, buvid, http), tokens));
    }
}
