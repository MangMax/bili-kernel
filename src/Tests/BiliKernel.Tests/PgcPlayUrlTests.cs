using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Services.Media;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 番剧播放地址请求形状测试。
/// </summary>
public sealed class PgcPlayUrlTests
{
    private const string NavResponse = @"{""code"":0,""data"":{""wbi_img"":{""img_url"":""https://i0.hdslb.com/bfs/wbi/7cd084941338484aae1ad9425b84077c.png"",""sub_url"":""https://i0.hdslb.com/bfs/wbi/4932caff0ff746eab6f01bf08b70ac45.png""}}}";
    private const string SpiResponse = @"{""code"":0,""data"":{""b_3"":""BUV3"",""b_4"":""BUV4""}}";
    private const string V2Response = @"{""code"":0,""data"":{""video_info"":{""accept_quality"":[64,32],""dash"":{""video"":[],""audio"":[]}}}}";
    private const string LegacyResponse = @"{""code"":0,""result"":{""accept_quality"":[64],""dash"":{""video"":[],""audio"":[]}}}";

    [Fact]
    public async Task PlayUrlUsesV2PathWithWbi()
    {
        var handler = CreateHandler(V2Response, LegacyResponse);
        var service = CreateService(handler);

        _ = await service.GetPgcPlayDetailAsync("123", "456", "1");

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
        Assert.Equal("/pgc/player/web/v2/playurl", request.Path);
        Assert.True(request.Query.ContainsKey("w_rid"));
        Assert.True(request.Query.ContainsKey("wts"));
        Assert.Equal("456", request.Query["ep_id"]);
    }

    [Fact]
    public async Task PlayUrlFallsBackToLegacyPathWithWbi()
    {
        var handler = CreateHandler(@"{""code"":-404,""message"":""not found""}", LegacyResponse);
        var service = CreateService(handler);

        _ = await service.GetPgcPlayDetailAsync("123", "456", "1");

        var requests = handler.Requests.Where(p => p.Path.Contains("playurl", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, requests.Count);
        Assert.Equal("/pgc/player/web/v2/playurl", requests[0].Path);
        Assert.Equal("/pgc/player/web/playurl", requests[1].Path);
        Assert.True(requests[0].Query.ContainsKey("w_rid"));
        Assert.True(requests[1].Query.ContainsKey("w_rid"));
    }

    private static RecordingHttpMessageHandler CreateHandler(string v2Json, string legacyJson)
        => new RecordingHttpMessageHandler()
            .With("/x/web-interface/nav", NavResponse)
            .With("/x/frontend/finger/spi", SpiResponse)
            .With("/pgc/player/web/v2/playurl", v2Json)
            .With("/pgc/player/web/playurl", legacyJson);

    private static PlayerService CreateService(RecordingHttpMessageHandler handler)
    {
        var cookies = new FakeCookiesResolver();
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        var authenticator = new BiliAuthenticator(cookies, new FakeTokenResolver(), buvid, http);
        return new PlayerService(http, authenticator, new FakeTokenResolver());
    }
}