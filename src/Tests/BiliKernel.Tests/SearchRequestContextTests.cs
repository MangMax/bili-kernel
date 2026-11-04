// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Services.Search;
using Xunit;

namespace Richasy.BiliKernel.Tests;

public sealed class SearchRequestContextTests
{
    private const string SearchPath = "/x/web-interface/wbi/search/type";

    [Theory]
    [InlineData(false, ComprehensiveSearchSortType.Default, null, "totalrank", "1")]
    [InlineData(true, ComprehensiveSearchSortType.Play, 2, "click", "2")]
    [InlineData(true, ComprehensiveSearchSortType.Danmaku, 3, "dm", "3")]
    [InlineData(true, ComprehensiveSearchSortType.Newest, 4, "pubdate", "4")]
    public async Task ComprehensiveSearchUsesWebContext(bool signedIn, ComprehensiveSearchSortType sort, int? page, string order, string expectedPage)
    {
        using var handler = new RecordingHttpMessageHandler();
        handler.With("/x/web-interface/nav", "{\"code\":0,\"data\":{\"wbi_img\":{\"img_url\":\"https://i0.hdslb.com/0123456789abcdef0123456789abcdef.png\",\"sub_url\":\"https://i0.hdslb.com/fedcba9876543210fedcba9876543210.png\"}}}");
        handler.With(SearchPath, "{\"code\":0,\"data\":{\"result\":[],\"numPages\":5}}");
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "synthetic-session" });
        var tokens = new FakeTokenResolver();
        if (signedIn)
        {
            tokens.Token = new BiliToken { AccessToken = "synthetic-token" };
        }

        var authenticator = new BiliAuthenticator(cookies, tokens, httpClient: http);
        var service = new SearchService(http, authenticator);
        var (videos, nextPage) = await service.GetComprehensiveSearchResultAsync("测试 & video", page, sort);

        Assert.Empty(videos);
        Assert.Equal((page ?? 1) + 1, nextPage);
        var request = Assert.Single(handler.Requests, r => r.Path == SearchPath);
        Assert.Equal("https://api.bilibili.com", request.Uri.GetLeftPart(UriPartial.Authority));
        Assert.True(request.Headers.TryGetValue("Origin", out var origin));
        Assert.Equal("https://search.bilibili.com", origin);
        Assert.Equal("https://search.bilibili.com/", request.Headers["Referer"]);
        Assert.Equal("SESSDATA=synthetic-session", request.Cookie);
        Assert.Equal("测试 & video", request.Query["keyword"]);
        Assert.Equal("video", request.Query["search_type"]);
        Assert.Equal(order, request.Query["order"]);
        Assert.Equal(expectedPage, request.Query["page"]);
        Assert.Equal("pc", request.Query["platform"]);
        Assert.Equal("1430654", request.Query["web_location"]);
        Assert.Equal("30", request.Query["page_size"]);
        Assert.False(request.Query.ContainsKey("build"));
        Assert.False(request.Query.ContainsKey("access_key"));
        Assert.False(request.Query.ContainsKey("sign"));
        Assert.Matches("^[0-9a-f]{32}$", request.Query["w_rid"]);
        Assert.True(long.Parse(request.Query["wts"]) > 0);

        // Re-sign the final wire parameters with the public authorizer and the
        // captured timestamp: catches parameters added/removed after signing.
        var wireParameters = request.Query.Where(p => p.Key != "w_rid").ToDictionary(p => p.Key, p => p.Value);
        var authorized = authenticator.AuthorizeRequestParameters(wireParameters, new BiliAuthorizeExecutionSettings
        {
            ApiType = BiliApiType.None,
            NeedRID = true,
            ForceNoToken = true,
            SkipBuild = true,
        });
        Assert.Equal(request.Query["w_rid"], authorized["w_rid"]);
        Assert.Equal(9, request.Query.Count);
    }

    [Theory]
    [InlineData(BiliApiType.App)]
    [InlineData(BiliApiType.Web)]
    [InlineData(BiliApiType.None)]
    public async Task DefaultAuthorizationPreservesAppParameters(BiliApiType apiType)
    {
        using var handler = new RecordingHttpMessageHandler();
        using var http = new BiliHttpClient(handler);
        var tokens = new FakeTokenResolver { Token = new BiliToken { AccessToken = "synthetic-token" } };
        var authenticator = new BiliAuthenticator(null, tokens, httpClient: http);
        using var message = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri("https://api.bilibili.com/test"));
        authenticator.AuthorizeRestRequest(message, new Dictionary<string, string> { ["id"] = "123" }, new BiliAuthorizeExecutionSettings { ApiType = apiType });

        using var response = await http.SendAsync(message);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("123", request.Query["id"]);
        Assert.Equal(BiliClientIdentity.BuildNumber, request.Query["build"]);
        Assert.Equal("synthetic-token", request.Query["access_key"]);
        Assert.Matches("^[0-9a-f]{32}$", request.Query["sign"]);
        Assert.False(request.Query.ContainsKey("w_rid"));
        if (apiType != BiliApiType.None)
        {
            Assert.Equal(apiType == BiliApiType.App ? "ios" : "web", request.Query["platform"]);
            Assert.False(string.IsNullOrEmpty(request.Query["appkey"]));
            Assert.True(long.Parse(request.Query["ts"]) > 0);
        }
    }
}
