// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media;
using RichasyKernel;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 直播二级分区列表的行为测试：真实服务 + 伪造出站请求。
/// </summary>
public sealed class LivePartitionTests
{
    private const string AppPath = "/xlive/app-interface/v2/second/getList";
    private const string WebPath = "/xlive/web-interface/v1/second/getList";
    private const string LegacyPath = "/room/v3/Area/getRoomList";
    private const string NavPath = "/x/web-interface/nav";
    private const string BuvidPath = "/x/frontend/finger/spi";

    private const string AppList = """{"code":0,"data":{"list":[{"roomid":1234,"uid":42,"uname":"Host","face":"https://example.com/face.jpg","title":"Network room","system_cover":"https://example.com/system.jpg","watched_show":{"num":321},"online":7,"area_v2_id":86,"area_v2_name":"网游","area_v2_parent_id":2}],"count":1,"has_more":1,"new_tags":[{"id":1,"name":"热门","sort_type":"online"}]}}""";

    private const string RiskControl = """{"code":-352,"message":"risk control"}""";

    private const string WebList = """{"code":0,"data":{"list":[{"room_id":5678,"uid":9,"uname":"WebHost","title":"Web room","cover":"https://example.com/web.jpg","online":88,"area_id":86,"area_name":"网游","parent_area_id":2}],"count":1,"has_more":1,"new_tags":[{"id":2,"name":"热门","sort_type":"online"}]}}""";

    [Fact]
    public async Task NetworkGamePartitionLoadsRoomsFromAppSecondList()
    {
        var (service, handler) = Create();

        var (lives, tags, nextPage) = await service.GetPartitionLiveListAsync(new Partition("0", "网游", parentId: "2"));

        var live = Assert.Single(lives);
        Assert.Equal("1234", live.Identifier.Id);
        Assert.Equal("Network room", live.Identifier.Title);
        Assert.Equal("Host", live.User.Name);
        Assert.Equal(321d, live.GetExtensionIfNotNull<double>(LiveExtensionDataId.ViewerCount));
        Assert.Equal("网游", live.GetExtensionIfNotNull<string>(LiveExtensionDataId.Subtitle));
        var tag = Assert.Single(tags!);
        Assert.Equal("online", tag.SortType);
        Assert.Equal(1, nextPage);

        var request = Assert.Single(handler.Requests, r => r.Path == AppPath);
        Assert.Equal("2", request.Query["parent_area_id"]);
        Assert.Equal("0", request.Query["area_id"]);
        Assert.Equal("1", request.Query["page"]);
        Assert.Equal("40", request.Query["page_size"]);
        Assert.Equal("1", request.Query["tag_version"]);
        Assert.Equal("2", request.Query["scale"]);
        Assert.True(request.Query.ContainsKey("appkey"));
        Assert.True(request.Query.ContainsKey("ts"));
        Assert.False(request.Query.ContainsKey("access_key"));
        var signedText = string.Join("&", request.Query.Where(p => p.Key != "sign").Select(p => $"{p.Key}={p.Value}").OrderBy(p => p, StringComparer.Ordinal));
        var expectedSign = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(signedText + "c2ed53a74eeefe3cf99fbd01d8c9c375"))).ToLowerInvariant();
        Assert.Equal(expectedSign, request.Query["sign"]);
        Assert.DoesNotContain(handler.Requests, r => r.Path == WebPath || r.Path == LegacyPath);
    }

    [Fact]
    public async Task AppRiskControlWithLoginCookieFallsBackToWebOnce()
    {
        var (service, handler) = Create(cookie: true, appJson: RiskControl, webJson: WebList);

        var (lives, tags, nextPage) = await service.GetPartitionLiveListAsync(new Partition("0", "网游", parentId: "2"));

        Assert.Equal("5678", Assert.Single(lives).Identifier.Id);
        Assert.Equal("online", Assert.Single(tags!).SortType);
        Assert.Equal(1, nextPage);
        var requests = handler.Requests.Where(r => r.Path == AppPath || r.Path == WebPath).ToList();
        Assert.Equal(2, requests.Count);
        Assert.Equal(AppPath, requests[0].Path);
        Assert.Equal(WebPath, requests[1].Path);
        var web = requests[1];
        Assert.Equal("2", web.Query["parent_area_id"]);
        Assert.Equal("0", web.Query["area_id"]);
        Assert.Equal("1", web.Query["page"]);
        Assert.Equal("40", web.Query["page_size"]);
        Assert.Equal("online", web.Query["sort_type"]);
        Assert.True(web.Query.ContainsKey("w_rid"));
        Assert.True(web.Query.ContainsKey("wts"));
        Assert.False(web.Query.ContainsKey("access_key"));
        Assert.Contains("SESSDATA=session", web.Cookie);
        Assert.DoesNotContain(handler.Requests, r => r.Path == LegacyPath);
    }

    [Fact]
    public async Task AppRiskControlWithoutLoginCookieFailsWithoutWebFallback()
    {
        var (service, handler) = Create(appJson: RiskControl);

        var error = await Assert.ThrowsAsync<KernelException>(() => service.GetPartitionLiveListAsync(new Partition("0", "网游", parentId: "2")));

        Assert.Contains("risk control", error.Message);
        Assert.Single(handler.Requests, r => r.Path == AppPath);
        Assert.DoesNotContain(handler.Requests, r => r.Path == WebPath || r.Path == LegacyPath);
    }

    [Fact]
    public async Task BothSecondListEndpointsRiskControlFailsWithoutLegacyFallback()
    {
        var (service, handler) = Create(cookie: true, appJson: RiskControl, webJson: """{"code":-352,"message":"web risk"}""");

        var error = await Assert.ThrowsAsync<KernelException>(() => service.GetPartitionLiveListAsync(new Partition("0", "网游", parentId: "2")));

        Assert.Contains("web risk", error.Message);
        var requests = handler.Requests.Where(r => r.Path == AppPath || r.Path == WebPath).ToList();
        Assert.Equal(2, requests.Count);
        Assert.Equal(AppPath, requests[0].Path);
        Assert.Equal(WebPath, requests[1].Path);
        Assert.DoesNotContain(handler.Requests, r => r.Path == LegacyPath);
    }

    [Fact]
    public async Task EmptyListIsALegalResultAndStillCarriesTags()
    {
        var (service, _) = Create(appJson: """{"code":0,"data":{"list":[],"count":0,"has_more":0,"new_tags":[{"id":1,"name":"热门","sort_type":"online"}]}}""");

        var (lives, tags, nextPage) = await service.GetPartitionLiveListAsync(new Partition("0", "网游", parentId: "2"));

        Assert.Empty(lives);
        Assert.Equal("online", Assert.Single(tags!).SortType);
        Assert.Equal(1, nextPage);
    }

    [Fact]
    public async Task NullListAndTagsAreTreatedAsEmptyCollections()
    {
        var (service, _) = Create(appJson: """{"code":0,"data":{"list":null,"count":0,"new_tags":null}}""");

        var (lives, tags, _) = await service.GetPartitionLiveListAsync(new Partition("0", "网游", parentId: "2"));

        Assert.Empty(lives);
        Assert.Empty(tags!);
    }

    [Fact]
    public async Task MissingDataIsAnErrorAndDoesNotFallBack()
    {
        var (service, handler) = Create(cookie: true, appJson: """{"code":0,"message":"ok"}""");

        var error = await Assert.ThrowsAsync<KernelException>(() => service.GetPartitionLiveListAsync(new Partition("0", "网游", parentId: "2")));

        Assert.Contains("直播分区列表数据缺失", error.Message);
        Assert.Single(handler.Requests, r => r.Path == AppPath);
        Assert.DoesNotContain(handler.Requests, r => r.Path == WebPath || r.Path == LegacyPath);
    }

    [Theory]
    [InlineData(-101, "not logged in")]
    [InlineData(-403, "forbidden")]
    [InlineData(-400, "bad request")]
    public async Task NonRiskAppErrorsDoNotFallBackEvenWithLoginCookie(int code, string message)
    {
        var (service, handler) = Create(cookie: true, appJson: $"{{\"code\":{code},\"message\":\"{message}\"}}");

        var error = await Assert.ThrowsAsync<KernelException>(() => service.GetPartitionLiveListAsync(new Partition("0", "网游", parentId: "2")));

        Assert.Contains(message, error.Message);
        Assert.Single(handler.Requests, r => r.Path == AppPath);
        Assert.DoesNotContain(handler.Requests, r => r.Path == WebPath || r.Path == LegacyPath);
    }

    [Fact]
    public async Task TagSortTypeIsPassedToAppRequestUnchanged()
    {
        var (service, handler) = Create();

        var (_, tags, nextPage) = await service.GetPartitionLiveListAsync(
            new Partition("0", "网游", parentId: "2"),
            new LiveTag("7", "最新", "live_time"),
            pageNumber: 1);

        Assert.Null(tags);
        Assert.Equal(2, nextPage);
        var request = Assert.Single(handler.Requests, r => r.Path == AppPath);
        Assert.Equal("2", request.Query["page"]);
        Assert.Equal("live_time", request.Query["sort_type"]);
    }

    [Fact]
    public async Task TagSortTypeIsPassedToWebFallbackRequestUnchanged()
    {
        var (service, handler) = Create(cookie: true, appJson: RiskControl, webJson: WebList);

        _ = await service.GetPartitionLiveListAsync(
            new Partition("0", "网游", parentId: "2"),
            new LiveTag("8", "萌新", "sort_type_2221"),
            pageNumber: 1);

        var requests = handler.Requests.Where(r => r.Path == AppPath || r.Path == WebPath).ToList();
        Assert.Equal(2, requests.Count);
        Assert.Equal("sort_type_2221", requests[0].Query["sort_type"]);
        Assert.Equal("sort_type_2221", requests[1].Query["sort_type"]);
    }

    [Fact]
    public async Task PaginationAdvancesPagesAndReturnsTagsOnlyOnTheFirstPage()
    {
        var (service, handler) = Create();
        var partition = new Partition("0", "网游", parentId: "2");

        var first = await service.GetPartitionLiveListAsync(partition);
        Assert.Equal(1, first.NextPageNumber);
        Assert.NotNull(first.Tags);

        var second = await service.GetPartitionLiveListAsync(partition, pageNumber: first.NextPageNumber);
        Assert.Equal(2, second.NextPageNumber);
        Assert.Null(second.Tags);

        var pages = handler.Requests.Where(r => r.Path == AppPath).Select(r => r.Query["page"]).ToList();
        Assert.Equal(["1", "2"], pages);
    }

    [Fact]
    public async Task RoomCardVariantsMapToLiveInformation()
    {
        var (service, _) = Create(appJson: """{"code":0,"data":{"list":[{"room_id":5678,"id":9999,"uid":9,"uname":"Variant","face":"https://example.com/face.jpg","title":"Variant room","system_cover":"https://example.com/system.jpg","cover":"https://example.com/cover.jpg","watched_show":{"num":777},"online":5,"area_v2_id":86,"area_v2_name":"网游","area_v2_parent_id":2}],"count":1,"has_more":0}}""");

        var (lives, tags, _) = await service.GetPartitionLiveListAsync(new Partition("0", "网游", parentId: "2"));

        var live = Assert.Single(lives);
        Assert.Equal("5678", live.Identifier.Id);
        Assert.Equal("Variant", live.User.Name);
        Assert.StartsWith("https://example.com/system.jpg", live.Identifier.Cover!.SourceUri.AbsoluteUri);
        Assert.Equal(777d, live.GetExtensionIfNotNull<double>(LiveExtensionDataId.ViewerCount));
        Assert.Equal("网游", live.GetExtensionIfNotNull<string>(LiveExtensionDataId.Subtitle));
        Assert.Empty(tags!);
    }

    [Fact]
    public async Task AppTransportFailureWithLoginCookieFallsBackToWebOnce()
    {
        var handler = CreateHandler(new RecordingHttpMessageHandler(request => request.Path switch
        {
            AppPath => throw new HttpRequestException("network down"),
            WebPath => RecordingHttpMessageHandler.Json(WebList),
            _ => RecordingHttpMessageHandler.Json("""{"code":0}"""),
        }));

        var (lives, _, _) = await CreateService(handler, cookie: true).GetPartitionLiveListAsync(new Partition("0", "网游", parentId: "2"));

        Assert.Equal("5678", Assert.Single(lives).Identifier.Id);
        var requests = handler.Requests.Where(r => r.Path == AppPath || r.Path == WebPath).ToList();
        Assert.Equal(2, requests.Count);
        Assert.Equal(AppPath, requests[0].Path);
        Assert.Equal(WebPath, requests[1].Path);
        Assert.DoesNotContain(handler.Requests, r => r.Path == LegacyPath);
    }

    [Fact]
    public async Task AppTransportFailureWithoutLoginCookieFailsWithoutWebFallback()
    {
        var handler = CreateHandler(new RecordingHttpMessageHandler(request => request.Path == AppPath
            ? throw new HttpRequestException("network down")
            : RecordingHttpMessageHandler.Json("""{"code":0}""")));

        var error = await Assert.ThrowsAsync<KernelException>(() => CreateService(handler, cookie: false).GetPartitionLiveListAsync(new Partition("0", "网游", parentId: "2")));

        Assert.Contains("network down", error.Message);
        Assert.Single(handler.Requests, r => r.Path == AppPath);
        Assert.DoesNotContain(handler.Requests, r => r.Path == WebPath || r.Path == LegacyPath);
    }

    private static (LiveDiscoveryService Service, RecordingHttpMessageHandler Handler) Create(bool cookie = false, string? appJson = null, string? webJson = null)
    {
        var handler = CreateHandler(new RecordingHttpMessageHandler()).With(AppPath, appJson ?? AppList);
        if (webJson != null)
        {
            handler.With(WebPath, webJson);
        }

        return (CreateService(handler, cookie), handler);
    }

    private static RecordingHttpMessageHandler CreateHandler(RecordingHttpMessageHandler handler)
        => handler
            .With(NavPath, """{"code":0,"data":{"wbi_img":{"img_url":"https://example.com/7cd084941338484aae1ad9425b84077c.png","sub_url":"https://example.com/4932caff0ff746eab6f01bf08b70ac45.png"}}}""")
            .With(BuvidPath, """{"code":0,"data":{"b_3":"B3","b_4":"B4"}}""");

    private static LiveDiscoveryService CreateService(RecordingHttpMessageHandler handler, bool cookie)
    {
        var cookies = new FakeCookiesResolver();
        if (cookie)
        {
            cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session" });
        }

        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        var tokens = new FakeTokenResolver();
        return new LiveDiscoveryService(http, new BiliAuthenticator(cookies, tokens, buvid, http), tokens, cookies);
    }
}
