// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili.Search;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Services.Search;
using Xunit;

namespace Richasy.BiliKernel.Tests;

public sealed class SearchVerificationTests
{
    private const string SearchPath = "/x/web-interface/wbi/search/type";
    private const string Video = "{\"aid\":123,\"bvid\":\"BV1xx411c7mD\",\"title\":\"A <em class=\\\"keyword\\\">video</em>\",\"pic\":\"//i0.hdslb.com/video.jpg\",\"mid\":456,\"author\":\"Publisher\",\"upic\":\"https://i0.hdslb.com/avatar.jpg\",\"duration\":\"01:23\",\"pubdate\":1700000000}";

    [Fact]
    public async Task ComprehensiveSearchVerificationRequiredThrowsTypedException()
    {
        using var handler = new RecordingHttpMessageHandler();
        handler.With("/x/web-interface/nav", WbiLifecycleTests.Nav);
        handler.With(SearchPath, "{\"code\":0,\"data\":{\"v_voucher\":\"" + Guid.NewGuid().ToString("N") + "\"}}");
        using var http = new BiliHttpClient(handler);
        ISearchService service = new SearchService(http, new BiliAuthenticator(new FakeCookiesResolver(), new FakeTokenResolver(), httpClient: http));

        await Assert.ThrowsAsync<SearchVerificationRequiredException>(() => service.GetComprehensiveSearchResultAsync("query"));
        Assert.Single(handler.Requests, request => request.Path == SearchPath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ComprehensiveSearchVerificationTakesPrecedenceOverResults(int responseCase)
    {
        using var handler = new RecordingHttpMessageHandler();
        handler.With("/x/web-interface/nav", WbiLifecycleTests.Nav);
        var results = responseCase == 1 ? "{}" : Video;
        var voucher = responseCase == 2 ? " " : Guid.NewGuid().ToString("N");
        handler.With(SearchPath, "{\"code\":0,\"data\":{\"v_voucher\":\"" + voucher + "\",\"result\":[" + results + "],\"numPages\":2}}");
        using var http = new BiliHttpClient(handler);
        var service = CreateService(http);

        await Assert.ThrowsAsync<SearchVerificationRequiredException>(() => service.GetComprehensiveSearchResultAsync("query"));
        Assert.Single(handler.Requests, request => request.Path == SearchPath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ComprehensiveSearchNormalEmptyRemainsEmpty(int voucherCase)
    {
        using var handler = new RecordingHttpMessageHandler();
        handler.With("/x/web-interface/nav", WbiLifecycleTests.Nav);
        handler.With(SearchPath, "{\"code\":0,\"data\":{" + EmptyVoucherField(voucherCase) + "\"result\":[],\"numPages\":0}}");
        using var http = new BiliHttpClient(handler);

        var (videos, nextPage) = await CreateService(http).GetComprehensiveSearchResultAsync("query");

        Assert.Empty(videos);
        Assert.Null(nextPage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ComprehensiveSearchNormalVideoAndPaginationRemainValid(int voucherCase)
    {
        using var handler = new RecordingHttpMessageHandler();
        handler.With("/x/web-interface/nav", WbiLifecycleTests.Nav);
        handler.With(SearchPath, "{\"code\":0,\"data\":{" + EmptyVoucherField(voucherCase) + "\"result\":[" + Video + "],\"numPages\":2}}");
        using var http = new BiliHttpClient(handler);

        var (videos, nextPage) = await CreateService(http).GetComprehensiveSearchResultAsync("query");

        var video = Assert.Single(videos);
        Assert.Equal("123", video.Identifier.Id);
        Assert.Equal("A video", video.Identifier.Title);
        Assert.Equal(83, video.Duration);
        Assert.Equal(2, nextPage);
    }

    [Fact]
    public async Task ComprehensiveSearchRecoversOnSubsequentExplicitRequest()
    {
        using var handler = new RecordingHttpMessageHandler();
        handler.With("/x/web-interface/nav", WbiLifecycleTests.Nav);
        handler.With(SearchPath, "{\"code\":0,\"data\":{\"v_voucher\":\"" + Guid.NewGuid().ToString("N") + "\"}}");
        using var http = new BiliHttpClient(handler);
        var service = CreateService(http);
        await Assert.ThrowsAsync<SearchVerificationRequiredException>(() => service.GetComprehensiveSearchResultAsync("query"));
        Assert.Single(handler.Requests, request => request.Path == SearchPath);

        handler.With(SearchPath, "{\"code\":0,\"data\":{\"result\":[" + Video + "],\"numPages\":1}}");
        var (videos, nextPage) = await service.GetComprehensiveSearchResultAsync("query");

        Assert.Equal("123", Assert.Single(videos).Identifier.Id);
        Assert.Null(nextPage);
        Assert.Equal(2, handler.Requests.Count(request => request.Path == SearchPath));
    }

    [Fact]
    public async Task ComprehensiveSearchVerificationExceptionDoesNotExposeSensitiveFields()
    {
        var sensitive = Guid.NewGuid().ToString("N");
        using var handler = new RecordingHttpMessageHandler();
        handler.With("/x/web-interface/nav", WbiLifecycleTests.Nav);
        handler.With(SearchPath, "{\"code\":0,\"message\":\"" + sensitive + "\",\"data\":{\"v_voucher\":\"" + sensitive + "\",\"extra\":\"" + sensitive + "\"}}");
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = sensitive });
        ISearchService service = new SearchService(http, new BiliAuthenticator(cookies, new FakeTokenResolver(), httpClient: http));

        var exception = await Assert.ThrowsAsync<SearchVerificationRequiredException>(() => service.GetComprehensiveSearchResultAsync(sensitive));

        Assert.IsAssignableFrom<RichasyKernel.KernelException>(exception);
        Assert.Equal("搜索需要完成验证，请稍后重试。", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Empty(exception.Data);
        Assert.False(exception.ToString().Contains(sensitive, StringComparison.Ordinal));
        Assert.False(exception.ToString().Contains("v_voucher", StringComparison.Ordinal));
    }

    private static ISearchService CreateService(BiliHttpClient http)
        => new SearchService(http, new BiliAuthenticator(new FakeCookiesResolver(), new FakeTokenResolver(), httpClient: http));

    private static string EmptyVoucherField(int voucherCase)
        => voucherCase switch
        {
            1 => "\"v_voucher\":null,",
            2 => "\"v_voucher\":\"\",",
            _ => string.Empty,
        };
}
