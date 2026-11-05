using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Services.Media;
using RichasyKernel;
using Xunit;

namespace Richasy.BiliKernel.Tests;

public sealed class LiveCredentialTests
{
    private const string DetailPath = "/xlive/web-room/v1/index/getInfoByRoom";
    private const string DanmakuPath = "/xlive/web-room/v1/index/getDanmuInfo";
    private const string Detail = """{"code":0,"data":{"room_info":{"room_id":1234,"uid":42,"title":"Live title","cover":"https://example.com/cover.jpg","area_name":"Games"},"anchor_info":{"base_info":{"uname":"Host","face":"https://example.com/face.jpg"}}}}""";

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task DetailPreservesRoomAndUsesOptionalCookieWithoutAppToken(bool cookie, bool token)
    {
        var (handler, service) = Create(cookie, token);
        var result = await service.GetLivePageDetailAsync(new MediaIdentifier("1234", null, null));
        Assert.Equal("1234", result.Information.Identifier.Id);
        Assert.Equal("Live title", result.Information.Identifier.Title);
        var request = Assert.Single(handler.Requests, r => r.Path == DetailPath);
        Assert.Equal("1234", request.Query["room_id"]);
        Assert.Equal("phone", request.Query["device"]);
        AssertCredentials(request, cookie);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DetailSuccessDoesNotHideDanmakuRejection(bool cookie)
    {
        var (handler, service) = Create(cookie, true);
        handler.With(DanmakuPath, """{"code":-101,"message":"chat denied"}""");
        Assert.NotNull(await service.GetLivePageDetailAsync(new MediaIdentifier("1234", null, null)));
        var error = await Assert.ThrowsAsync<KernelException>(() => service.EnterLiveRoomAsync("1234", CancellationToken.None));
        Assert.Contains("chat denied", error.Message);
        var request = Assert.Single(handler.Requests, r => r.Path == DanmakuPath);
        Assert.Equal("1234", request.Query["id"]);
        Assert.Equal("0", request.Query["type"]);
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

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task SuccessfulDanmakuResponseWithMissingHostFailsExplicitly(bool cookie, bool token)
    {
        var (handler, service) = Create(cookie, token);
        handler.With(DanmakuPath, """{"code":0,"data":{"token":"chat-token","host_list":[]}}""");
        var error = await Assert.ThrowsAsync<KernelException>(() => service.EnterLiveRoomAsync("1234", CancellationToken.None));
        Assert.Equal("无法获取直播弹幕服务器地址", error.Message);
        AssertCredentials(Assert.Single(handler.Requests, r => r.Path == DanmakuPath), cookie);
    }
    [Fact]
    public async Task DanmakuSuccessConsumesHostRoomAndTokenIndependentlyOfDetail()
    {
        var (handler, service) = Create(true, true);
        handler.With(DetailPath, """{"code":-403,"message":"detail denied"}""");
        handler.With(DanmakuPath, """{"code":0,"data":{"token":"synthetic-chat-key","host_list":[{"host":"chat.example.test"}]}}""");
        handler.With("/x/frontend/finger/spi", """{"code":0,"data":{"b_3":"B3","b_4":"B4"}}""");
        var connected = false;
        service.ConnectLiveSocket = (uri, authentication, cancellationToken) =>
        {
            Assert.Equal("wss://chat.example.test/sub", uri.AbsoluteUri);
            Assert.Equal(7, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(authentication.AsSpan(8, 4)));
            using var payload = System.Text.Json.JsonDocument.Parse(authentication.AsMemory(16));
            Assert.Equal(1234, payload.RootElement.GetProperty("roomid").GetInt32());
            Assert.Equal("synthetic-chat-key", payload.RootElement.GetProperty("key").GetString());
            Assert.Equal(2, payload.RootElement.GetProperty("protover").GetInt32());
            connected = true;
            return Task.FromResult(new System.Net.WebSockets.ClientWebSocket());
        };
        using var socket = await service.EnterLiveRoomAsync("1234", CancellationToken.None);
        Assert.True(connected);
        Assert.DoesNotContain(handler.Requests, r => r.Path == DetailPath);
        var request = Assert.Single(handler.Requests, r => r.Path == DanmakuPath);
        Assert.Equal("1234", request.Query["id"]);
        Assert.Equal("0", request.Query["type"]);
        AssertCredentials(request, true);
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
