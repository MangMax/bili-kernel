// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Services.User;
using Xunit;

using Richasy.BiliKernel.Models.Authorization;

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

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task SpaceInformationUsesWbiEndpoint(bool hasToken, bool hasCookie)
    {
        var (handler, service) = CreateService(hasToken, hasCookie);

        var card = await service.GetUserSpaceInformationAsync("1").ConfigureAwait(true);

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("/x/space/wbi/acc/info", StringComparison.Ordinal));
        Assert.True(request.Query.ContainsKey("w_rid"));
        Assert.True(request.Query.ContainsKey("wts"));
        var signedQuery = string.Join("&", request.Query.Where(p => p.Key != "w_rid").OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        var expected = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(signedQuery + "ea1db124af3c7062474693fa704f4ff8"))).ToLowerInvariant();
        Assert.Equal(expected, request.Query["w_rid"]);
        Assert.False(request.Query.ContainsKey("access_key"));
        if (hasCookie)
        {
            Assert.Contains("SESSDATA=space-session", request.Cookie);
        }
        else
        {
            Assert.DoesNotContain("SESSDATA=", request.Cookie ?? string.Empty);
        }
        Assert.Equal("1", request.Query["mid"]);
        AssertAntiCrawlParameters(request);
        AssertSpaceHeaders(request, "/1");
        Assert.Equal("up", card.Profile?.User.Name);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task SpaceVideosUseWbiEndpoint(bool hasToken, bool hasCookie)
    {
        var (handler, service) = CreateService(hasToken, hasCookie);

        var (videos, total) = await service.GetUserVideosAsync("1", 1);

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("/x/space/wbi/arc/search", StringComparison.Ordinal));
        Assert.True(request.Query.ContainsKey("w_rid"));
        Assert.True(request.Query.ContainsKey("wts"));
        var signedQuery = string.Join("&", request.Query.Where(p => p.Key != "w_rid").OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        var expected = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(signedQuery + "ea1db124af3c7062474693fa704f4ff8"))).ToLowerInvariant();
        Assert.Equal(expected, request.Query["w_rid"]);
        Assert.False(request.Query.ContainsKey("access_key"));
        if (hasCookie)
        {
            Assert.Contains("SESSDATA=space-session", request.Cookie);
        }
        else
        {
            Assert.DoesNotContain("SESSDATA=", request.Cookie ?? string.Empty);
        }
        Assert.Equal("1", request.Query["mid"]);
        Assert.Equal("2", request.Query["pn"]);
        AssertAntiCrawlParameters(request);
        AssertSpaceHeaders(request, "/1/video");
        Assert.Equal("t", Assert.Single(videos).Identifier.Title);
        Assert.Equal(1, total);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task SpaceArticlesUseWbiEndpoint(bool hasToken, bool hasCookie)
    {
        var (handler, service) = CreateService(hasToken, hasCookie);

        var articles = await service.GetUserArticlesAsync("1", 1);

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("/x/space/wbi/article", StringComparison.Ordinal));
        Assert.True(request.Query.ContainsKey("w_rid"));
        Assert.True(request.Query.ContainsKey("wts"));
        var signedQuery = string.Join("&", request.Query.Where(p => p.Key != "w_rid").OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        var expected = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(signedQuery + "ea1db124af3c7062474693fa704f4ff8"))).ToLowerInvariant();
        Assert.Equal(expected, request.Query["w_rid"]);
        Assert.False(request.Query.ContainsKey("access_key"));
        if (hasCookie)
        {
            Assert.Contains("SESSDATA=space-session", request.Cookie);
        }
        else
        {
            Assert.DoesNotContain("SESSDATA=", request.Cookie ?? string.Empty);
        }
        Assert.Equal("2", request.Query["pn"]);
        Assert.Equal("1", request.Query["mid"]);
        AssertAntiCrawlParameters(request);
        AssertSpaceHeaders(request, "/1/article");
        Assert.Single(articles);
        Assert.Equal("a", articles[0].Identifier.Title);
    }

    [Theory]
    [InlineData("info")]
    [InlineData("videos")]
    [InlineData("articles")]
    public async Task RejectedCookieIsNotReportedAsEmptySpace(string operation)
    {
        var (handler, service) = CreateService();
        handler.With(operation == "info" ? "/x/space/wbi/acc/info" : operation == "videos" ? "/x/space/wbi/arc/search" : "/x/space/wbi/article", "{\"code\":-101,\"message\":\"not logged in\"}");
        await Assert.ThrowsAsync<RichasyKernel.KernelException>(async () =>
        {
            if (operation == "info") await service.GetUserSpaceInformationAsync("1").ConfigureAwait(true);
            else if (operation == "videos") await service.GetUserVideosAsync("1").ConfigureAwait(true);
            else await service.GetUserArticlesAsync("1").ConfigureAwait(true);
        });
        var request = Assert.Single(handler.Requests, p => p.Path.StartsWith("/x/space/wbi/", StringComparison.Ordinal));
        Assert.False(request.Query.ContainsKey("access_key"));
        Assert.Contains("SESSDATA=space-session", request.Cookie);
    }

    [Fact]
    public async Task ExistingAppMutationStillCarriesTokenAndSign()
    {
        var handler = new RecordingHttpMessageHandler();
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session" });
        var tokens = new FakeTokenResolver { Token = new BiliToken { AccessToken = "app-token" } };
        var http = new BiliHttpClient(handler);
        var service = new ViewLaterService(http, new FakeAuthenticationService(), new BiliAuthenticator(cookies, tokens, httpClient: http));
        await service.AddAsync("123");
        var request = Assert.Single(handler.Requests);
        Assert.Contains("access_key=app-token", request.Body);
        Assert.Contains("aid=123", request.Body);
        Assert.Contains("sign=", request.Body);
    }

    [Fact]
    public async Task ExistingWebHistoryStillExcludesAppToken()
    {
        var handler = new RecordingHttpMessageHandler().With("/x/web-interface/history/cursor", @"{""code"":0,""data"":{""list"":[{""title"":""t"",""cover"":""https://i0.hdslb.com/a.jpg"",""duration"":10,""bvid"":""BV1xx411c7mD"",""history"":{""oid"":1,""business"":""archive""}}],""cursor"":{""max"":99}}}");
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session" });
        var tokens = new FakeTokenResolver { Token = new BiliToken { AccessToken = "app-token" } };
        var http = new BiliHttpClient(handler);
        var service = new ViewHistoryService(http, new FakeAuthenticationService(), new BiliAuthenticator(cookies, tokens, httpClient: http));
        var result = await service.GetViewHistoryAsync(Richasy.BiliKernel.Models.ViewHistoryTabType.Video, 0);
        Assert.Equal(99, result.Offset);
        Assert.Equal("t", Assert.Single(result.Videos).Identifier.Title);
        var request = Assert.Single(handler.Requests);
        Assert.False(request.Query.ContainsKey("access_key"));
        Assert.Contains("SESSDATA=session", request.Cookie);
        Assert.True(request.Query.ContainsKey("sign"));
    }

    private sealed class RejectTokenRefresh : Richasy.BiliKernel.Bili.Authorization.IAuthenticationService
    {
        public Task SignInAsync(Richasy.BiliKernel.Bili.AuthorizeExecutionSettings? settings = default, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unrelated token refresh");
        public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureTokenAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unrelated token refresh");
    }

    private static void AssertAntiCrawlParameters(RecordedRequest request)
    {
        Assert.Equal("[]", request.Query["dm_img_list"]);
        Assert.False(string.IsNullOrEmpty(request.Query["dm_img_str"]));
        Assert.False(string.IsNullOrEmpty(request.Query["dm_cover_img_str"]));
        Assert.False(string.IsNullOrEmpty(request.Query["dm_img_inter"]));
    }

    private static void AssertSpaceHeaders(RecordedRequest request, string refererPath)
    {
        Assert.Equal($"https://space.bilibili.com{refererPath}", request.Headers["Referer"]);
        Assert.Equal("https://space.bilibili.com", request.Headers["Origin"]);
    }

    private static (RecordingHttpMessageHandler Handler, UserService Service) CreateService(bool hasToken = true, bool hasCookie = true)
    {
        var handler = new RecordingHttpMessageHandler()
            .With("/x/web-interface/nav", NavResponse)
            .With("/x/frontend/finger/spi", SpiResponse)
            .With("/x/space/wbi/acc/info", InfoResponse)
            .With("/x/space/wbi/arc/search", VideosResponse)
            .With("/x/space/wbi/article", ArticlesResponse);
        var cookies = new FakeCookiesResolver();
        if (hasCookie) cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "space-session", ["bili_jct"] = "space-csrf" });
        var tokens = new FakeTokenResolver { Token = hasToken ? new BiliToken { AccessToken = "app-token" } : null };
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        var authenticator = new BiliAuthenticator(cookies, tokens, buvid, http);
        return (handler, new UserService(http, new RejectTokenRefresh(), tokens, authenticator));
    }
}
