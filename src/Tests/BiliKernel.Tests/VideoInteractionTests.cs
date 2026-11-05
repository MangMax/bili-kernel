// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

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
        Assert.Contains("bili_jct=csrf-token", request.Cookie);
        Assert.DoesNotContain("access_key", request.Body);
        Assert.False(request.Query.ContainsKey("access_key"));

        await service.ToggleVideoLikeAsync("42", false);
        var unlike = handler.Requests.Last(p => p.Path.Contains("archive/like", StringComparison.Ordinal));
        Assert.Contains("like=2", unlike.Body, StringComparison.Ordinal);
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
        Assert.Contains("bili_jct=csrf-token", request.Cookie);
        Assert.DoesNotContain("access_key", request.Body);
        Assert.False(request.Query.ContainsKey("access_key"));
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
        Assert.Contains("bili_jct=csrf-token", request.Cookie);
        Assert.DoesNotContain("access_key", request.Body);
        Assert.False(request.Query.ContainsKey("access_key"));
    }

    [Fact]
    public async Task TripleIsComposedOfLikeAndCoin()
    {
        var (handler, service) = CreateService();

        await service.TripleVideoAsync("42");

        Assert.Contains(handler.Requests, p => p.Path.Contains("archive/like", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, p => p.Path.Contains("coin/add", StringComparison.Ordinal));
        Assert.Equal(2, handler.Requests.Count(p => !p.Path.Contains("gaia-gateway", StringComparison.Ordinal)));
        Assert.Contains("multiply=2", handler.Requests.Last().Body);
        Assert.Contains("select_like=1", handler.Requests.Last().Body);
        Assert.DoesNotContain(handler.Requests, p => p.Path.Contains("fav/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("like")]
    [InlineData("coin")]
    [InlineData("favorite")]
    public async Task RejectedMutationPropagatesFailure(string action)
    {
        var (handler, service) = CreateService();
        handler.With("/x/", "{\"code\":-101,\"message\":\"not signed in\"}");
        await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => action switch
        {
            "like" => service.ToggleVideoLikeAsync("42", true),
            "coin" => service.CoinVideoAsync("42", 1, false),
            _ => service.FavoriteVideoAsync("42", new List<string> { "10" }, new List<string>()),
        });
        Assert.DoesNotContain("access_key", Assert.Single(handler.Requests, p => !p.Path.Contains("gaia-gateway", StringComparison.Ordinal)).Body);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task CookieOnlyWorksWithoutAppTokenAndPreservesResourceParameters(bool hasCookie, bool hasToken)
    {
        var (handler, service) = CreateService(hasToken: hasToken, hasCookie: hasCookie);
        await service.FavoriteEpisodeAsync("42", new List<string> { "10", "11" }, new List<string> { "12" });
        var request = Assert.Single(handler.Requests, p => !p.Path.Contains("gaia-gateway", StringComparison.Ordinal));
        Assert.Contains("type=24", request.Body);
        Assert.Contains("add_media_ids=10%2C11", request.Body);
        Assert.Contains("del_media_ids=12", request.Body);
        Assert.DoesNotContain("access_key", request.Body);
        if (hasCookie)
        {
            Assert.Contains("SESSDATA=session", request.Cookie);
            Assert.Contains("csrf=csrf-token", request.Body);
        }
        else
        {
            Assert.DoesNotContain("SESSDATA", request.Cookie);
            Assert.Contains("csrf=", request.Body);
        }
    }

    [Fact]
    public async Task SingleCoinDoesNotImplicitlyLike()
    {
        var (handler, service) = CreateService();
        await service.CoinVideoAsync("42", 1, false);
        var request = Assert.Single(handler.Requests, p => !p.Path.Contains("gaia-gateway", StringComparison.Ordinal));
        Assert.Contains("multiply=1", request.Body);
        Assert.DoesNotContain("select_like", request.Body);
    }
    private static (RecordingHttpMessageHandler Handler, PlayerService Service) CreateService(bool hasToken = true, bool hasCookie = true)
    {
        var handler = new RecordingHttpMessageHandler();
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "bili_jct", "csrf-token" },
            { "SESSDATA", "session" },
        });
        if (!hasCookie) cookies.RemoveCookies();
        var tokens = new FakeTokenResolver { Token = hasToken ? new Richasy.BiliKernel.Models.Authorization.BiliToken { AccessToken = "app-token" } : null };
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        var authenticator = new BiliAuthenticator(cookies, tokens, buvid, http);
        return (handler, new PlayerService(http, authenticator, tokens));
    }
}
