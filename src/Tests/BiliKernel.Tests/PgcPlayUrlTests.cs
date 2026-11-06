// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

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
    private const string V2Response = @"{""code"":0,""data"":{""video_info"":{""timelength"":1381396,""accept_quality"":[64,32],""dash"":{""video"":[],""audio"":[]}}}}";
    private const string LegacyResponse = @"{""code"":0,""result"":{""accept_quality"":[64],""dash"":{""video"":[],""audio"":[]}}}";

    [Fact]
    public async Task PlayUrlUsesV2PathWithWbi()
    {
        var handler = CreateHandler(V2Response, LegacyResponse);
        var service = CreateService(handler);

        var dash = await service.GetPgcPlayDetailAsync("123", "456", "1");

        Assert.Equal(1381, dash.Duration);
        var request = Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
        Assert.Equal("/pgc/player/web/v2/playurl", request.Path);
        Assert.True(request.Query.ContainsKey("w_rid"));
        Assert.True(request.Query.ContainsKey("wts"));
        Assert.Equal("456", request.Query["ep_id"]);
        Assert.DoesNotContain("access_key", request.Query);
        Assert.Contains("SESSDATA=session", request.Cookie);
    }

    [Fact]
    public async Task PlayUrlFallsBackToLegacyPathWithWbi()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("{}", System.Net.HttpStatusCode.NotFound))
            .With("/x/web-interface/nav", NavResponse)
            .With("/pgc/player/web/playurl", LegacyResponse);
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

    [Theory]
    [InlineData(-101)]
    [InlineData(-403)]
    [InlineData(-404)]
    [InlineData(-10403)]
    public async Task PrimaryRejectionCannotBeHiddenBySuccessfulLegacy(int code)
    {
        var handler = CreateHandler($"{{\"code\":{code},\"message\":\"primary denied\"}}", LegacyResponse);
        var error = await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => CreateService(handler).GetPgcPlayDetailAsync("123", "456", "1"));
        Assert.Contains("primary denied", error.Message);
        Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnsupportedPrimaryFormatUsesLegacyDash()
    {
        var handler = CreateHandler("{\"code\":0,\"data\":{\"video_info\":{\"durl\":[{\"url\":\"https://media.invalid/format.flv\"}]}}}", LegacyResponse);
        var result = await CreateService(handler).GetPgcPlayDetailAsync("123", "456", "1");
        Assert.NotNull(result);
        Assert.Equal(2, handler.Requests.Count(p => p.Path.Contains("playurl", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(64, false)]
    [InlineData(112, false)]
    [InlineData(64, true)]
    [InlineData(112, true)]
    public async Task OrdinaryAndEntitledFixturesPreserveStreamsAndPermissions(int quality, bool fallback)
    {
        // Synthetic account results, not claims about live server entitlement.
        var payload = $$$"""
            {"timelength":120000,"support_formats":[{"quality":{{{quality}}},"new_description":"fixture"}],
             "dash":{"video":[{"id":{{{quality}}},"base_url":"https://media.invalid/{{{quality}}}.m4s","segment_base":{"initialization":"0-100","index_range":"101-200"}}],"audio":[]}}
            """;
        var handler = new RecordingHttpMessageHandler(request => request.Path.EndsWith("/v2/playurl", StringComparison.Ordinal)
            ? fallback ? RecordingHttpMessageHandler.Json("{}", System.Net.HttpStatusCode.NotFound)
                : RecordingHttpMessageHandler.Json("{\"code\":0,\"" + (quality == 112 ? "result" : "data") + "\":{\"video_info\":" + payload + "}}")
            : RecordingHttpMessageHandler.Json("{\"code\":0,\"result\":" + payload + "}"))
            .With("/x/web-interface/nav", NavResponse);

        var result = await CreateService(handler).GetPgcPlayDetailAsync("123", "456", "2");
        var video = Assert.Single(result.Videos!);
        Assert.Equal(quality.ToString(), video.Id);
        Assert.Equal($"https://media.invalid/{quality}.m4s", video.BaseUrl);
        Assert.Equal(120, result.Duration);
        var format = Assert.Single(result.Formats!);
        Assert.Equal(quality, format.Quality);
        Assert.Equal(quality == 112, format.NeedVip);
        Assert.Equal(quality == 112, format.NeedLogin);
        var requests = handler.Requests.Where(p => p.Path.Contains("playurl", StringComparison.Ordinal)).ToList();
        Assert.Equal(fallback ? 2 : 1, requests.Count);
        Assert.Equal("/pgc/player/web/v2/playurl", requests[0].Path);
        if (fallback) Assert.Equal("/pgc/player/web/playurl", requests[1].Path);
        Assert.All(requests, request => AssertPlaybackRequest(request, "2"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task OptionalCredentialsAllowPublicPlayback(bool hasCookie, bool hasToken)
    {
        var handler = CreateHandler(V2Response, LegacyResponse);
        Assert.NotNull(await CreateService(handler, hasCookie, hasToken).GetPgcPlayDetailAsync("123", "456", "1"));
        var request = Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
        Assert.DoesNotContain("access_key", request.Query);
        Assert.Equal(hasCookie, request.Cookie?.Contains("SESSDATA=session", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrExpiredCookieDoesNotUseLegacySuccess(bool expired)
    {
        var handler = CreateHandler("{\"code\":-101,\"message\":\"login required\"}", LegacyResponse);
        var error = await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => CreateService(handler, hasCookie: expired).GetPgcPlayDetailAsync("123", "456", "1"));
        Assert.Contains("login required", error.Message);
        var request = Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
        Assert.DoesNotContain("access_key", request.Query);
        Assert.Equal(expired, request.Cookie?.Contains("SESSDATA=session", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData(-101)]
    [InlineData(-10403)]
    [InlineData(-404)]
    public async Task LegacyRejectionIsNotReportedAsPlayback(int code)
    {
        var handler = new RecordingHttpMessageHandler(request => request.Path.EndsWith("/v2/playurl", StringComparison.Ordinal)
            ? RecordingHttpMessageHandler.Json("{}", System.Net.HttpStatusCode.NotFound)
            : RecordingHttpMessageHandler.Json($"{{\"code\":{code},\"message\":\"legacy denied\"}}"))
            .With("/x/web-interface/nav", NavResponse);
        var error = await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => CreateService(handler).GetPgcPlayDetailAsync("123", "456", "1"));
        Assert.Contains("legacy denied", error.Message);
        var requests = handler.Requests.Where(p => p.Path.Contains("playurl", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => AssertPlaybackRequest(request, "1"));
    }

    [Fact]
    public async Task MissingPlaybackPayloadDoesNotMasqueradeAsFormatFallback()
    {
        var handler = CreateHandler("{\"code\":0,\"result\":{\"view_info\":{\"dialog\":{\"type\":\"pay\"}}}}", LegacyResponse);
        await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => CreateService(handler).GetPgcPlayDetailAsync("123", "456", "1"));
        Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
    }

    private static void AssertPlaybackRequest(RecordedRequest request, string seasonType)
    {
        Assert.DoesNotContain("access_key", request.Query);
        Assert.Contains("SESSDATA=session", request.Cookie);
        Assert.Equal("123", request.Query["cid"]);
        Assert.Equal("456", request.Query["ep_id"]);
        Assert.Equal(seasonType, request.Query["season_type"]);
        Assert.Equal("64", request.Query["qn"]);
        Assert.Equal("4048", request.Query["fnval"]);
        Assert.Equal("1", request.Query["fourk"]);
        Assert.Equal("1", request.Query["try_look"]);
        Assert.DoesNotContain("csrf", request.Query);
        Assert.DoesNotContain("sign", request.Query);
        Assert.True(long.TryParse(request.Query["wts"], out _));
        var signedQuery = string.Join("&", request.Query.Where(p => p.Key != "w_rid").OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        var expected = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(signedQuery + "ea1db124af3c7062474693fa704f4ff8"))).ToLowerInvariant();
        Assert.Equal(expected, request.Query["w_rid"]);
    }

    [Fact]
    public async Task CookiePlaybackNeverConsultsUnrelatedAppToken()
    {
        var handler = CreateHandler(V2Response, LegacyResponse);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session" });
        var http = new BiliHttpClient(handler);
        var tokens = new UnavailableTokenResolver();
        var service = new PlayerService(http, new BiliAuthenticator(cookies, tokens, httpClient: http), tokens);
        Assert.NotNull(await service.GetPgcPlayDetailAsync("123", "456", "1"));
        AssertPlaybackRequest(Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal)), "1");
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("transport")]
    [InlineData("cancelled")]
    public async Task NonCompatibilityFailuresDoNotUseLegacy(string failure)
    {
        var handler = new RecordingHttpMessageHandler(_ => failure switch
        {
            "malformed" => RecordingHttpMessageHandler.Json("not json"),
            "transport" => throw new HttpRequestException("offline"),
            _ => throw new OperationCanceledException(),
        }).With("/x/web-interface/nav", NavResponse);
        await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => CreateService(handler).GetPgcPlayDetailAsync("123", "456", "1"));
        Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
    }

    private sealed class UnavailableTokenResolver : Richasy.BiliKernel.Bili.Authorization.IBiliTokenResolver
    {
        public Richasy.BiliKernel.Models.Authorization.BiliToken? GetToken() => throw new InvalidOperationException("Unrelated App token unavailable");
        public void SaveToken(Richasy.BiliKernel.Models.Authorization.BiliToken token) => throw new NotSupportedException();
        public void RemoveToken() => throw new NotSupportedException();
    }

    private static PlayerService CreateService(RecordingHttpMessageHandler handler, bool hasCookie = true, bool hasToken = true)
    {
        var cookies = new FakeCookiesResolver();
        if (hasCookie) cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session" });
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        var tokens = new FakeTokenResolver { Token = hasToken ? new() { AccessToken = "app-token" } : null };
        var authenticator = new BiliAuthenticator(cookies, tokens, buvid, http);
        return new PlayerService(http, authenticator, tokens);
    }
}
