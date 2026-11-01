// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Services.User;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 稍后再看与观看历史请求形状测试。
/// </summary>
public sealed class ViewLaterHistoryTests
{
    private const string NavResponse = @"{""code"":0,""data"":{""wbi_img"":{""img_url"":""https://i0.hdslb.com/bfs/wbi/7cd084941338484aae1ad9425b84077c.png"",""sub_url"":""https://i0.hdslb.com/bfs/wbi/4932caff0ff746eab6f01bf08b70ac45.png""}}}";
    private const string SpiResponse = @"{""code"":0,""data"":{""b_3"":""BUV3"",""b_4"":""BUV4""}}";
    private const string ViewLaterResponse = @"{""code"":0,""data"":{""count"":1,""list"":[]}}";
    private const string HistoryResponse = @"{""code"":0,""data"":{""list"":[{""title"":""t"",""cover"":""https://i0.hdslb.com/a.jpg"",""duration"":10,""bvid"":""BV1xx411c7mD"",""history"":{""oid"":1,""business"":""archive""}}],""cursor"":{""max"":99}}}";

    [Fact]
    public async Task ViewLaterListUsesWebEndpointWithWbi()
    {
        var handler = CreateHandler();
        var service = CreateViewLaterService(handler);

        _ = await service.GetViewLaterSetAsync(0, CancellationToken.None);

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("toview", StringComparison.Ordinal));
        Assert.Equal("/x/v2/history/toview/web", request.Path);
        Assert.True(request.Query.ContainsKey("w_rid"));
        Assert.True(request.Query.ContainsKey("wts"));
    }

    [Fact]
    public async Task HistoryListUsesWebCursorEndpoint()
    {
        var handler = CreateHandler();
        var service = CreateViewHistoryService(handler);

        var group = await service.GetViewHistoryAsync(ViewHistoryTabType.Video, 0, CancellationToken.None);

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("history/cursor", StringComparison.Ordinal));
        Assert.Equal("/x/web-interface/history/cursor", request.Path);
        Assert.Single(group.Videos);
        Assert.Equal(99, group.Offset);
    }

    [Fact]
    public async Task HistoryCleanUsesWebEndpointWithCsrf()
    {
        var handler = CreateHandler();
        var service = CreateViewHistoryService(handler);

        await service.CleanHistoryAsync(ViewHistoryTabType.Video, CancellationToken.None);

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("history/clear", StringComparison.Ordinal));
        Assert.Equal("/x/v2/history/clear", request.Path);
        Assert.Contains("csrf=csrf-token", request.Body, StringComparison.Ordinal);
    }

    private static RecordingHttpMessageHandler CreateHandler()
        => new RecordingHttpMessageHandler()
            .With("/x/web-interface/nav", NavResponse)
            .With("/x/frontend/finger/spi", SpiResponse)
            .With("/x/v2/history/toview/web", ViewLaterResponse)
            .With("/x/web-interface/history/cursor", HistoryResponse)
            .With("/x/v2/history/clear", @"{""code"":0}");

    private static (BiliHttpClient Http, BiliAuthenticator Authenticator) CreateCore(RecordingHttpMessageHandler handler)
    {
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "bili_jct", "csrf-token" },
        });
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        var authenticator = new BiliAuthenticator(cookies, new FakeTokenResolver(), buvid, http);
        return (http, authenticator);
    }

    private static ViewLaterService CreateViewLaterService(RecordingHttpMessageHandler handler)
    {
        var (http, authenticator) = CreateCore(handler);
        return new ViewLaterService(http, new FakeAuthenticationService(), authenticator);
    }

    private static ViewHistoryService CreateViewHistoryService(RecordingHttpMessageHandler handler)
    {
        var (http, authenticator) = CreateCore(handler);
        return new ViewHistoryService(http, new FakeAuthenticationService(), authenticator);
    }
}