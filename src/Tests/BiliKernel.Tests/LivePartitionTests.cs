// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 直播二级分区请求形状测试.
/// </summary>
public sealed class LivePartitionTests
{
    private const string AreaDetailResponse = """
        {"code":0,"data":{"count":1,"new_tags":[{"id":4289,"name":"综合","sort_type":""},{"id":4290,"name":"云顶之弈","sort_type":"sort_type_4290"}],"list":[{"roomid":123,"uid":9,"uname":"主播","face":"","cover":"","title":"直播间","area_name":"英雄联盟","watched_show":{"num":5}}]}}
        """;

    private const string EmptyResponse = """
        {"code":0,"data":{"count":0,"new_tags":[{"id":2221,"name":"热门","sort_type":"sort_type_2221"}],"list":[]}}
        """;

    private const string NullListResponse = """
        {"code":0,"data":{"count":0,"new_tags":[{"id":4289,"name":"综合","sort_type":""}],"list":null}}
        """;

    private const string NullTagsResponse = """
        {"code":0,"data":{"count":1,"new_tags":null,"list":[{"roomid":123,"uid":9,"uname":"主播","face":"","cover":"","title":"直播间","area_name":"英雄联盟","watched_show":{"num":5}}]}}
        """;

    [Fact]
    public async Task PartitionWithoutTagRequestsOnlineSort()
    {
        var handler = new RecordingHttpMessageHandler().With("/room/v3/Area/getRoomList", AreaDetailResponse);
        var service = CreateService(handler);

        var (lives, tags, _) = await service.GetPartitionLiveListAsync(new Partition("86", "英雄联盟", parentId: "2"));

        var request = Assert.Single(handler.Requests, p => p.Path == "/room/v3/Area/getRoomList");
        Assert.Equal("online", request.Query["sort_type"]);
        Assert.Equal("86", request.Query["area_id"]);
        Assert.Equal("2", request.Query["parent_area_id"]);
        Assert.Equal("1", request.Query["page"]);
        Assert.Single(lives);
        Assert.Equal(2, tags!.Count);
    }

    [Fact]
    public async Task EmptyTagSortTypeFallsBackToOnline()
    {
        var handler = new RecordingHttpMessageHandler().With("/room/v3/Area/getRoomList", AreaDetailResponse);
        var service = CreateService(handler);

        await service.GetPartitionLiveListAsync(new Partition("86", "英雄联盟", parentId: "2"), new LiveTag("4289", "综合", string.Empty));

        var request = Assert.Single(handler.Requests, p => p.Path == "/room/v3/Area/getRoomList");
        Assert.Equal("online", request.Query["sort_type"]);
    }

    [Fact]
    public async Task TagSortTypeIsForwarded()
    {
        var handler = new RecordingHttpMessageHandler().With("/room/v3/Area/getRoomList", AreaDetailResponse);
        var service = CreateService(handler);

        await service.GetPartitionLiveListAsync(new Partition("86", "英雄联盟", parentId: "2"), new LiveTag("4290", "云顶之弈", "sort_type_4290"));

        var request = Assert.Single(handler.Requests, p => p.Path == "/room/v3/Area/getRoomList");
        Assert.Equal("sort_type_4290", request.Query["sort_type"]);
    }

    [Fact]
    public async Task EmptyTagResultRetriesWithOnline()
    {
        var handler = new RecordingHttpMessageHandler(request =>
            RecordingHttpMessageHandler.Json(request.Query["sort_type"] == "online" ? AreaDetailResponse : EmptyResponse));
        var service = CreateService(handler);

        var (lives, _, _) = await service.GetPartitionLiveListAsync(new Partition("0", "全部网游", parentId: "2"), new LiveTag("2221", "热门", "sort_type_2221"));

        Assert.Single(lives);
        var requests = handler.Requests.Where(p => p.Path == "/room/v3/Area/getRoomList").ToList();
        Assert.Equal(2, requests.Count);
        Assert.Equal("sort_type_2221", requests[0].Query["sort_type"]);
        Assert.Equal("online", requests[1].Query["sort_type"]);
    }

    [Fact]
    public async Task EmptyPartitionReturnsEmptyList()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(EmptyResponse));
        var service = CreateService(handler);

        var (lives, tags, _) = await service.GetPartitionLiveListAsync(new Partition("0", "全部网游", parentId: "2"));

        Assert.Empty(lives);
        Assert.NotNull(tags);
    }

    [Fact]
    public async Task NullRoomListReturnsEmptyCollection()
    {
        var handler = new RecordingHttpMessageHandler().With("/room/v3/Area/getRoomList", NullListResponse);
        var service = CreateService(handler);

        var (lives, tags, _) = await service.GetPartitionLiveListAsync(new Partition("0", "全部网游", parentId: "2"));

        Assert.Empty(lives);
        Assert.Single(tags!);
    }

    [Fact]
    public async Task NullTagListReturnsEmptyCollection()
    {
        var handler = new RecordingHttpMessageHandler().With("/room/v3/Area/getRoomList", NullTagsResponse);
        var service = CreateService(handler);

        var (lives, tags, _) = await service.GetPartitionLiveListAsync(new Partition("0", "全部网游", parentId: "2"));

        Assert.Single(lives);
        Assert.Empty(tags!);
    }

    private static LiveDiscoveryService CreateService(RecordingHttpMessageHandler handler)
    {
        var cookies = new FakeCookiesResolver();
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        var tokens = new FakeTokenResolver { Token = new BiliToken { AccessToken = "app-token" } };
        var authenticator = new BiliAuthenticator(cookies, tokens, buvid, http);
        return new LiveDiscoveryService(http, authenticator, tokens);
    }
}
