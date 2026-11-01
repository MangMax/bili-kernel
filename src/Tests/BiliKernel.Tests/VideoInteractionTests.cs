using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Services.Media;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 视频互动请求形状测试。
/// </summary>
public sealed class VideoInteractionTests
{
    [Fact]
    public async Task LikeUsesWebEndpointWithCsrf()
    {
        var (handler, service) = CreateService();

        await service.ToggleVideoLikeAsync("42", true);

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("archive/like", StringComparison.Ordinal));
        Assert.Equal("/x/web-interface/archive/like", request.Path);
        Assert.Equal("POST", request.Method);
        Assert.Contains("like=1", request.Body, StringComparison.Ordinal);
        Assert.Contains("csrf=csrf-token", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CoinUsesWebEndpointWithCsrf()
    {
        var (handler, service) = CreateService();

        await service.CoinVideoAsync("42", 2, true);

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("coin/add", StringComparison.Ordinal));
        Assert.Equal("/x/web-interface/coin/add", request.Path);
        Assert.Contains("multiply=2", request.Body, StringComparison.Ordinal);
        Assert.Contains("select_like=1", request.Body, StringComparison.Ordinal);
        Assert.Contains("csrf=csrf-token", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FavoriteUsesDealEndpointWithCsrf()
    {
        var (handler, service) = CreateService();

        await service.FavoriteVideoAsync("42", new List<string> { "10" }, new List<string>());

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("fav/resource/deal", StringComparison.Ordinal));
        Assert.Equal("/x/v3/fav/resource/deal", request.Path);
        Assert.Contains("rid=42", request.Body, StringComparison.Ordinal);
        Assert.Contains("type=2", request.Body, StringComparison.Ordinal);
        Assert.Contains("add_media_ids=10", request.Body, StringComparison.Ordinal);
        Assert.Contains("csrf=csrf-token", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TripleIsComposedOfLikeAndCoin()
    {
        var (handler, service) = CreateService();

        await service.TripleVideoAsync("42");

        Assert.Contains(handler.Requests, p => p.Path.Contains("archive/like", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, p => p.Path.Contains("coin/add", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Requests, p => p.Path.Contains("triple", StringComparison.Ordinal));
    }

    private static (RecordingHttpMessageHandler Handler, PlayerService Service) CreateService()
    {
        var handler = new RecordingHttpMessageHandler();
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "bili_jct", "csrf-token" },
        });
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        var authenticator = new BiliAuthenticator(cookies, new FakeTokenResolver(), buvid, http);
        return (handler, new PlayerService(http, authenticator, new FakeTokenResolver()));
    }
}