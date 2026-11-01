using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Services.User;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 用户空间请求形状测试。
/// </summary>
public sealed class SpaceTests
{
    private const string NavResponse = @"{""code"":0,""data"":{""wbi_img"":{""img_url"":""https://i0.hdslb.com/bfs/wbi/7cd084941338484aae1ad9425b84077c.png"",""sub_url"":""https://i0.hdslb.com/bfs/wbi/4932caff0ff746eab6f01bf08b70ac45.png""}}}";
    private const string SpiResponse = @"{""code"":0,""data"":{""b_3"":""BUV3"",""b_4"":""BUV4""}}";
    private const string InfoResponse = @"{""code"":0,""data"":{""mid"":1,""name"":""up"",""face"":""https://i0.hdslb.com/face.jpg"",""sign"":""hi"",""level"":6,""vip"":{""status"":1}}}";
    private const string VideosResponse = @"{""code"":0,""data"":{""list"":{""vlist"":[{""aid"":1,""bvid"":""BV1xx411c7mD"",""title"":""t"",""pic"":""https://i0.hdslb.com/a.jpg"",""created"":1600000000,""play"":10,""video_review"":2,""length"":""01:30""}]},""page"":{""count"":1}}}";
    private const string ArticlesResponse = @"{""code"":0,""data"":{""articles"":[{""id"":1,""title"":""a"",""summary"":""s"",""image_urls"":[""https://i0.hdslb.com/b.jpg""],""publish_time"":1600000000}]}}";

    [Fact]
    public async Task SpaceInformationUsesWbiEndpoint()
    {
        var (handler, service) = CreateService();

        var card = await service.GetUserSpaceInformationAsync("1");

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("/x/space/wbi/acc/info", StringComparison.Ordinal));
        Assert.True(request.Query.ContainsKey("w_rid"));
        Assert.Equal("1", request.Query["mid"]);
        Assert.Equal("up", card.Profile?.User.Name);
    }

    [Fact]
    public async Task SpaceVideosUseWbiEndpoint()
    {
        var (handler, service) = CreateService();

        var (videos, total) = await service.GetUserVideosAsync("1", 2);

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("/x/space/wbi/arc/search", StringComparison.Ordinal));
        Assert.True(request.Query.ContainsKey("w_rid"));
        Assert.Equal("1", request.Query["mid"]);
        Assert.Equal("2", request.Query["pn"]);
        Assert.Single(videos);
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task SpaceArticlesUseWbiEndpoint()
    {
        var (handler, service) = CreateService();

        var articles = await service.GetUserArticlesAsync("1", 1);

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("/x/space/wbi/article", StringComparison.Ordinal));
        Assert.True(request.Query.ContainsKey("w_rid"));
        Assert.Single(articles);
        Assert.Equal("a", articles[0].Identifier.Title);
    }

    private static (RecordingHttpMessageHandler Handler, UserService Service) CreateService()
    {
        var handler = new RecordingHttpMessageHandler()
            .With("/x/web-interface/nav", NavResponse)
            .With("/x/frontend/finger/spi", SpiResponse)
            .With("/x/space/wbi/acc/info", InfoResponse)
            .With("/x/space/wbi/arc/search", VideosResponse)
            .With("/x/space/wbi/article", ArticlesResponse);
        var cookies = new FakeCookiesResolver();
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        var authenticator = new BiliAuthenticator(cookies, new FakeTokenResolver(), buvid, http);
        return (handler, new UserService(http, new FakeAuthenticationService(), new FakeTokenResolver(), authenticator));
    }
}