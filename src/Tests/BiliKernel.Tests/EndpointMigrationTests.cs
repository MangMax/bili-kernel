// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media;
using Richasy.BiliKernel.Services.Search;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 端点迁移后的请求形状测试。
/// </summary>
public sealed class EndpointMigrationTests
{
    [Fact]
    public async Task SeasonDetailUsesWebEndpoint()
    {
        var handler = CreateHandler();
        var service = CreatePlayerService(handler);

        _ = await Record.ExceptionAsync(() => service.GetPgcPageDetailAsync("123", null, CancellationToken.None));

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("season", StringComparison.Ordinal));
        Assert.Equal("/pgc/view/web/season", request.Path);
        Assert.Equal("123", request.Query["season_id"]);
    }

    [Fact]
    public async Task AnimeTimelineUsesWebEndpoint()
    {
        var handler = CreateHandler();
        var service = CreateEntertainmentService(handler);

        _ = await Record.ExceptionAsync(() => service.GetBangumiTimelineAsync(CancellationToken.None));

        var request = handler.Requests.First(p => p.Path.Contains("timeline", StringComparison.Ordinal));
        Assert.Equal("/pgc/web/timeline", request.Path);
        Assert.Equal("1", request.Query["types"]);
    }

    [Fact]
    public async Task AnimeTimelineFallsBackToLegacyEndpoint()
    {
        var handler = CreateHandler();
        handler.With("/pgc/web/timeline", @"{""code"":-400,""message"":""bad""}");
        var service = CreateEntertainmentService(handler);

        _ = await Record.ExceptionAsync(() => service.GetBangumiTimelineAsync(CancellationToken.None));

        var requests = handler.Requests.Where(p => p.Path.Contains("timeline", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, requests.Count);
        Assert.Equal("/pgc/web/timeline", requests[0].Path);
        Assert.Equal("/pgc/app/timeline", requests[1].Path);
    }

    [Fact]
    public async Task FollowUsesWebEndpoint()
    {
        var handler = CreateHandler();
        var service = CreateEntertainmentService(handler);

        _ = await Record.ExceptionAsync(() => service.FollowAsync("123", CancellationToken.None));

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("follow", StringComparison.Ordinal));
        Assert.Equal("/pgc/web/follow/add", request.Path);
        Assert.Equal("POST", request.Method);
    }

    [Fact]
    public async Task PagedPartitionVideosUseWebRegionEndpoint()
    {
        var handler = CreateHandler();
        var service = CreateVideoDiscoveryService(handler);

        _ = await Record.ExceptionAsync(() => service.GetChildPartitionVideoListAsync(new Partition("1", "动画"), 0, 1, PartitionVideoSortType.Play, CancellationToken.None));

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("region", StringComparison.Ordinal) || p.Path.Contains("newlist", StringComparison.Ordinal));
        Assert.Equal("/x/web-interface/dynamic/region", request.Path);
        Assert.Equal("1", request.Query["rid"]);
        Assert.Equal("2", request.Query["pn"]);
        Assert.Equal("30", request.Query["ps"]);
        Assert.Equal("view", request.Query["order"]);
    }

    [Fact]
    public async Task PagedPartitionVideosFallBackToNewList()
    {
        var handler = CreateHandler();
        handler.With("/x/web-interface/dynamic/region", @"{""code"":-404,""message"":""missing""}");
        var service = CreateVideoDiscoveryService(handler);

        _ = await Record.ExceptionAsync(() => service.GetChildPartitionVideoListAsync(new Partition("1", "动画"), 0, 1, PartitionVideoSortType.Play, CancellationToken.None));

        var requests = handler.Requests.Where(p => p.Path.Contains("region", StringComparison.Ordinal) || p.Path.Contains("newlist", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, requests.Count);
        Assert.Equal("/x/web-interface/dynamic/region", requests[0].Path);
        Assert.Equal("/x/web-interface/newlist", requests[1].Path);
    }

    [Fact]
    public async Task LiveRoomDetailUsesWebRoomEndpoint()
    {
        var handler = CreateHandler();
        var service = CreatePlayerService(handler);

        _ = await Record.ExceptionAsync(() => service.GetLivePageDetailAsync(new MediaIdentifier("1", "room", null), CancellationToken.None));

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("getInfoByRoom", StringComparison.Ordinal));
        Assert.Equal("/xlive/web-room/v1/index/getInfoByRoom", request.Path);
    }

    [Fact]
    public async Task HotSearchUsesSearchSquareEndpoint()
    {
        var handler = CreateHandler();
        handler.With("/x/web-interface/search/square", @"{""code"":0,""data"":{""trending"":{""list"":[{""keyword"":""k"",""show_name"":""k"",""position"":1}]}}}");
        var service = CreateSearchService(handler);

        _ = await Record.ExceptionAsync(() => service.GetTotalHotSearchAsync(10, CancellationToken.None));

        var request = Assert.Single(handler.Requests, p => p.Path.Contains("square", StringComparison.Ordinal));
        Assert.Equal("/x/web-interface/search/square", request.Path);
        Assert.Equal("10", request.Query["limit"]);
    }

    private static RecordingHttpMessageHandler CreateHandler()
        => new RecordingHttpMessageHandler()
            .With("/x/frontend/finger/spi", @"{""code"":0,""data"":{""b_3"":""BUV3"",""b_4"":""BUV4""}}")
            .With("/x/web-interface/nav", @"{""code"":0,""data"":{""wbi_img"":{""img_url"":""https://i0.hdslb.com/bfs/wbi/7cd084941338484aae1ad9425b84077c.png"",""sub_url"":""https://i0.hdslb.com/bfs/wbi/4932caff0ff746eab6f01bf08b70ac45.png""}}}");

    private static (BiliHttpClient Http, BiliAuthenticator Authenticator) CreateCore(RecordingHttpMessageHandler handler)
    {
        var cookies = new FakeCookiesResolver();
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        var authenticator = new BiliAuthenticator(cookies, new FakeTokenResolver(), buvid, http);
        return (http, authenticator);
    }

    private static PlayerService CreatePlayerService(RecordingHttpMessageHandler handler)
    {
        var (http, authenticator) = CreateCore(handler);
        return new PlayerService(http, authenticator, new FakeTokenResolver());
    }

    private static EntertainmentDiscoveryService CreateEntertainmentService(RecordingHttpMessageHandler handler)
    {
        var (http, authenticator) = CreateCore(handler);
        return new EntertainmentDiscoveryService(http, authenticator);
    }

    private static VideoDiscoveryService CreateVideoDiscoveryService(RecordingHttpMessageHandler handler)
    {
        var (http, authenticator) = CreateCore(handler);
        return new VideoDiscoveryService(http, authenticator, new FakeTokenResolver());
    }

    private static SearchService CreateSearchService(RecordingHttpMessageHandler handler)
    {
        var (http, authenticator) = CreateCore(handler);
        return new SearchService(http, authenticator);
    }
}