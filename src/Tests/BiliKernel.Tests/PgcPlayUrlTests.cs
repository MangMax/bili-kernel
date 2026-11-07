// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Media;
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

    // 服务端在缺少账号身份时返回的试看响应：code 0，只有 durl，没有 dash。
    private const string PreviewResponse = @"{""code"":0,""data"":{""video_info"":{""durl"":[{""url"":""https://media.invalid/preview.flv""}]}}}";
    private const string LiveDetailResponse = @"{""code"":0,""data"":{""room_info"":{""room_id"":1234,""uid"":42,""title"":""Live title"",""cover"":""https://example.com/cover.jpg"",""area_name"":""Games""},""anchor_info"":{""base_info"":{""uname"":""Host"",""face"":""https://example.com/face.jpg""}}}}";

    [Fact]
    public async Task PlayUrlUsesV2PathWithWbi()
    {
        var handler = CreateHandler(V2Response, LegacyResponse);
        var service = CreateService(handler);

        var dash = await service.GetPgcPlayDetailAsync("123", "456", "1");

        Assert.Equal(1381, dash.Duration);
        var request = Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
        Assert.Equal("/pgc/player/web/v2/playurl", request.Path);
        AssertPlaybackRequest(request, "1");
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
        Assert.All(requests, request => AssertPlaybackRequest(request, "1"));
    }

    [Fact]
    public async Task PreviewResponseReturnsPlayableUrlsWithoutFallback()
    {
        const string preview = """
            {"code":0,"result":{"play_check":{"play_detail":"PLAY_PREVIEW"},
             "video_info":{"is_preview":1,"timelength":1381396,"accept_quality":[64,32],
              "support_formats":[{"quality":64,"new_description":"高清"}],
              "durl":[{"url":"https://media.invalid/preview.flv"}]}}}
            """;
        var handler = CreateHandler(preview, LegacyResponse);

        var dash = await CreateService(handler).GetPgcPlayDetailAsync("123", "456", "1");

        Assert.True(dash.IsPreview);
        Assert.Equal("PLAY_PREVIEW", dash.PlayCheck);
        Assert.Equal(["https://media.invalid/preview.flv"], dash.PreviewUrls);
        var format = Assert.Single(dash.Formats!);
        Assert.Equal(64, format.Quality);
        Assert.Equal(1381, dash.Duration);
        Assert.Null(dash.Videos);
        Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PreviewWithoutPlayableUrlsStillReportsPreview()
    {
        const string preview = """
            {"code":0,"result":{"play_check":{"play_detail":"PLAY_PREVIEW"},
             "video_info":{"timelength":60000,"is_preview":1,
              "support_formats":[{"quality":64,"new_description":"高清"}]}}}
            """;
        var handler = CreateHandler(preview, LegacyResponse);

        var dash = await CreateService(handler).GetPgcPlayDetailAsync("123", "456", "1");

        Assert.True(dash.IsPreview);
        Assert.Equal("PLAY_PREVIEW", dash.PlayCheck);
        Assert.Null(dash.PreviewUrls);
        Assert.Null(dash.Videos);
        Assert.Equal(60, dash.Duration);
        Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WholeEpisodeReturnsDashWithPlayCheck()
    {
        const string whole = """
            {"code":0,"result":{"play_check":{"play_detail":"PLAY_WHOLE"},
             "video_info":{"timelength":120000,"support_formats":[{"quality":64,"new_description":"高清"}],
              "dash":{"video":[{"id":64,"base_url":"https://media.invalid/64.m4s","segment_base":{"initialization":"0-100","index_range":"101-200"}}],"audio":[]}}}}
            """;
        var handler = CreateHandler(whole, LegacyResponse);

        var dash = await CreateService(handler).GetPgcPlayDetailAsync("123", "456", "1");

        Assert.False(dash.IsPreview);
        Assert.Equal("PLAY_WHOLE", dash.PlayCheck);
        Assert.Null(dash.PreviewUrls);
        Assert.Equal("https://media.invalid/64.m4s", Assert.Single(dash.Videos!).BaseUrl);
        var format = Assert.Single(dash.Formats!);
        Assert.Equal(64, format.Quality);
        Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
    }

    private static RecordingHttpMessageHandler CreateHandler(string v2Json, string legacyJson)
        => new RecordingHttpMessageHandler()
            .With("/x/web-interface/nav", NavResponse)
            .With("/x/frontend/finger/spi", SpiResponse)
            .With("/pgc/player/web/v2/playurl", v2Json)
            .With("/pgc/player/web/playurl", legacyJson);

    [Theory]
    [InlineData(-101, PgcPlaybackUnavailableReason.LoginRequired)]
    [InlineData(-403, PgcPlaybackUnavailableReason.NotEntitled)]
    [InlineData(-404, PgcPlaybackUnavailableReason.ContentUnavailable)]
    [InlineData(-10403, PgcPlaybackUnavailableReason.RegionRestricted)]
    public async Task PrimaryRejectionCannotBeHiddenBySuccessfulLegacy(int code, PgcPlaybackUnavailableReason reason)
    {
        var handler = CreateHandler($"{{\"code\":{code},\"message\":\"primary denied\"}}", LegacyResponse);
        var error = await Assert.ThrowsAsync<PgcPlaybackUnavailableException>(() => CreateService(handler).GetPgcPlayDetailAsync("123", "456", "1"));
        Assert.Equal(reason, error.Reason);
        Assert.Equal(code, error.Code);
        Assert.Contains("primary denied", error.Message);
        Assert.Contains(code.ToString(), error.Message);
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
    [InlineData(true, true)]
    public async Task OptionalCredentialsAllowPublicPlayback(bool hasCookie, bool hasToken)
    {
        var handler = CreateHandler(V2Response, LegacyResponse);
        Assert.NotNull(await CreateService(handler, hasCookie, hasToken).GetPgcPlayDetailAsync("123", "456", "1"));
        var request = Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
        Assert.Equal(hasToken, request.Query.ContainsKey("access_key"));
        Assert.Equal(hasCookie, request.Cookie?.Contains("SESSDATA=session", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "expired")]
    public async Task AppTokenRestoresDashWhenCookieIsMissingOrExpired(bool hasCookie, string? sessData)
    {
        // 模拟服务端：有 access_key 时返回完整 DASH，否则静默降级为试看响应。
        var handler = new RecordingHttpMessageHandler(request => request.Query.ContainsKey("access_key")
            ? RecordingHttpMessageHandler.Json(request.Path.EndsWith("/v2/playurl", StringComparison.Ordinal) ? V2Response : LegacyResponse)
            : RecordingHttpMessageHandler.Json(PreviewResponse))
            .With("/x/web-interface/nav", NavResponse);

        var dash = await CreateService(handler, hasCookie, hasToken: true, sessData).GetPgcPlayDetailAsync("123", "456", "1");

        Assert.Equal(1381, dash.Duration);
        var request = Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
        AssertPlaybackQuery(request, "1");
        Assert.Equal(hasCookie, request.Cookie?.Contains("SESSDATA=", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrExpiredCookieDoesNotUseLegacySuccess(bool expired)
    {
        var handler = CreateHandler("{\"code\":-101,\"message\":\"login required\"}", LegacyResponse);
        var error = await Assert.ThrowsAsync<PgcPlaybackUnavailableException>(() => CreateService(handler, hasCookie: expired).GetPgcPlayDetailAsync("123", "456", "1"));
        Assert.Equal(PgcPlaybackUnavailableReason.LoginRequired, error.Reason);
        Assert.Contains("login required", error.Message);
        var request = Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
        Assert.Equal("app-token", request.Query["access_key"]);
        Assert.Equal(expired, request.Cookie?.Contains("SESSDATA=session", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData(-101, PgcPlaybackUnavailableReason.LoginRequired)]
    [InlineData(-10403, PgcPlaybackUnavailableReason.RegionRestricted)]
    [InlineData(-404, PgcPlaybackUnavailableReason.ContentUnavailable)]
    public async Task LegacyRejectionIsNotReportedAsPlayback(int code, PgcPlaybackUnavailableReason reason)
    {
        var handler = new RecordingHttpMessageHandler(request => request.Path.EndsWith("/v2/playurl", StringComparison.Ordinal)
            ? RecordingHttpMessageHandler.Json("{}", System.Net.HttpStatusCode.NotFound)
            : RecordingHttpMessageHandler.Json($"{{\"code\":{code},\"message\":\"legacy denied\"}}"))
            .With("/x/web-interface/nav", NavResponse);
        var error = await Assert.ThrowsAsync<PgcPlaybackUnavailableException>(() => CreateService(handler).GetPgcPlayDetailAsync("123", "456", "1"));
        Assert.Equal(reason, error.Reason);
        Assert.Equal(code, error.Code);
        Assert.Contains("legacy denied", error.Message);
        var requests = handler.Requests.Where(p => p.Path.Contains("playurl", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => AssertPlaybackRequest(request, "1"));
    }

    [Fact]
    public async Task PayDialogReportsNotEntitledWithoutFallback()
    {
        var handler = CreateHandler("{\"code\":0,\"result\":{\"view_info\":{\"dialog\":{\"type\":\"pay\"}}}}", LegacyResponse);
        var error = await Assert.ThrowsAsync<PgcPlaybackUnavailableException>(() => CreateService(handler).GetPgcPlayDetailAsync("123", "456", "1"));
        Assert.Equal(PgcPlaybackUnavailableReason.NotEntitled, error.Reason);
        Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PgcPlaybackConsultsAppTokenAlongsideCookie()
    {
        var handler = CreateHandler(V2Response, LegacyResponse);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session" });
        var http = new BiliHttpClient(handler);
        var tokens = new FakeTokenResolver { Token = new() { AccessToken = "app-token" } };
        var service = new PlayerService(http, new BiliAuthenticator(cookies, tokens, httpClient: http), tokens);
        Assert.NotNull(await service.GetPgcPlayDetailAsync("123", "456", "1"));
        AssertPlaybackRequest(Assert.Single(handler.Requests, p => p.Path.Contains("playurl", StringComparison.Ordinal)), "1");
    }

    [Fact]
    public async Task OtherWebEndpointsStillOmitAppToken()
    {
        var handler = new RecordingHttpMessageHandler()
            .With("/x/web-interface/nav", NavResponse)
            .With("/xlive/web-room/v1/index/getInfoByRoom", LiveDetailResponse);

        var result = await CreateService(handler).GetLivePageDetailAsync(new MediaIdentifier("1234", null, null));

        Assert.Equal("Live title", result.Information.Identifier.Title);
        var request = Assert.Single(handler.Requests, p => p.Path == "/xlive/web-room/v1/index/getInfoByRoom");
        Assert.DoesNotContain("access_key", request.Query);
        Assert.Contains("SESSDATA=session", request.Cookie);
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

    private static void AssertPlaybackRequest(RecordedRequest request, string seasonType)
    {
        Assert.Equal("app-token", request.Query["access_key"]);
        Assert.Contains("SESSDATA=session", request.Cookie);
        AssertPlaybackQuery(request, seasonType);
    }

    private static void AssertPlaybackQuery(RecordedRequest request, string seasonType)
    {
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

    private static PlayerService CreateService(RecordingHttpMessageHandler handler, bool hasCookie = true, bool hasToken = true, string sessData = "session")
    {
        var cookies = new FakeCookiesResolver();
        if (hasCookie) cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = sessData });
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        var tokens = new FakeTokenResolver { Token = hasToken ? new() { AccessToken = "app-token" } : null };
        var authenticator = new BiliAuthenticator(cookies, tokens, buvid, http);
        return new PlayerService(http, authenticator, tokens);
    }
}
