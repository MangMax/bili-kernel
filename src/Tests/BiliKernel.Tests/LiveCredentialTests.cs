// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media;
using RichasyKernel;
using Xunit;

namespace Richasy.BiliKernel.Tests;

public sealed class LiveCredentialTests
{
    private const string DetailPath = "/xlive/web-room/v1/index/getInfoByRoom";
    private const string Detail = """{"code":0,"data":{"room_info":{"room_id":1234,"uid":42,"title":"Live title","cover":"https://example.com/cover.jpg","area_name":"Games"},"anchor_info":{"base_info":{"uname":"Host","face":"https://example.com/face.jpg"}}}}""";

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task DetailPreservesRoomAndUsesOptionalCookieWithoutAppToken(bool cookie, bool token)
    {
        var (handler, service) = Create(cookie, token);
        var result = await service.GetLivePageDetailAsync(new MediaIdentifier("1234", null, null)).ConfigureAwait(true);
        Assert.Equal("1234", result.Information.Identifier.Id);
        Assert.Equal("Live title", result.Information.Identifier.Title);
        var request = Assert.Single(handler.Requests, r => r.Path == DetailPath);
        Assert.Equal("1234", request.Query["room_id"]);
        Assert.Equal("phone", request.Query["device"]);
        AssertCredentials(request, cookie);
    }

    [Fact]
    public async Task DetailRejectionPropagates()
    {
        var (handler, service) = Create(true, true);
        handler.With(DetailPath, """{"code":-403,"message":"room denied"}""");
        var error = await Assert.ThrowsAsync<KernelException>(() => service.GetLivePageDetailAsync(new MediaIdentifier("1234", null, null)));
        Assert.Contains("room denied", error.Message);
        AssertCredentials(Assert.Single(handler.Requests, r => r.Path == DetailPath), true);
    }

    private static void AssertCredentials(RecordedRequest request, bool cookie)
    {
        Assert.False(request.Query.ContainsKey("access_key"));
        Assert.True(request.Query.ContainsKey("w_rid"));
        Assert.True(request.Query.ContainsKey("wts"));
        Assert.Equal("444.8", request.Query["web_location"]);
        Assert.Contains("buvid3=B3", request.Cookie);
        Assert.Equal(cookie, request.Cookie?.Contains("SESSDATA=session", StringComparison.Ordinal) == true);
    }

    private static (RecordingHttpMessageHandler, PlayerService) Create(bool cookie, bool token)
    {
        var handler = new RecordingHttpMessageHandler()
            .With("/x/web-interface/nav", """{"code":0,"data":{"wbi_img":{"img_url":"https://example.com/7cd084941338484aae1ad9425b84077c.png","sub_url":"https://example.com/4932caff0ff746eab6f01bf08b70ac45.png"}}}""")
            .With(DetailPath, Detail);
        var cookies = new FakeCookiesResolver();
        if (cookie) cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session" });
        var tokens = new FakeTokenResolver { Token = token ? new BiliToken { AccessToken = "app-token" } : null };
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        return (handler, new PlayerService(http, new BiliAuthenticator(cookies, tokens, buvid, http), tokens));
    }
}