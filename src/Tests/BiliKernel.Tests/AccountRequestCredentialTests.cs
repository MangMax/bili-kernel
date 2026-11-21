// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Services.Media;
using Richasy.BiliKernel.Services.User;
using RichasyKernel;
using System.Net;
using Xunit;

namespace Richasy.BiliKernel.Tests;

public sealed class AccountRequestCredentialTests
{
    [Theory]
    [InlineData(1, false, true)]
    [InlineData(2, true, false)]
    public async Task FollowingSearchUsesTheActiveAccountsWebSessionAndReturnsPagination(int page, bool androidHd, bool expectedHasMore)
    {
        using var handler = new RecordingHttpMessageHandler().With("/x/relation/followings/search",
            """{"code":0,"data":{"total":51,"list":[{"mid":23,"uname":"小可","face":"https://example.com/avatar.jpg","attribute":2,"vip":{"vipStatus":0}}]}}""");
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "test-session" });
        var tokens = new FakeTokenResolver { IsAndroidHdToken = androidHd, Token = new BiliToken { UserId = 42, AccessToken = "app-token" } };
        var service = new RelationshipService(http, new FakeAuthenticationService(), tokens, new BiliAuthenticator(cookies, tokens));

        var (users, hasMore) = await service.SearchMyFollowingsAsync("小 & 可", page);

        Assert.Equal(UserRelationStatus.Following, Assert.Single(users).Community.Relation);
        Assert.Equal(expectedHasMore, hasMore);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("42", request.Query["vmid"]);
        Assert.Equal("小 & 可", request.Query["name"]);
        Assert.Equal(page.ToString(), request.Query["pn"]);
        Assert.Equal("SESSDATA=test-session", request.Cookie);
        Assert.False(request.Query.ContainsKey("access_key"));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task PgcTrackingUsesTheAccountStatusEndpoint(int follow, bool expected)
    {
        using var handler = new RecordingHttpMessageHandler().With("/pgc/view/web/season/user/status",
            $$$"""{"code":0,"result":{"follow":{{{follow}}} }}""");
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "test-session" });
        var tokens = new FakeTokenResolver { IsAndroidHdToken = true, Token = new BiliToken { AccessToken = "app-token" } };
        var service = new EntertainmentDiscoveryService(http, new BiliAuthenticator(cookies, tokens));

        Assert.Equal(expected, await service.GetPgcTrackingStateAsync("28747"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal("28747", request.Query["season_id"]);
        Assert.Equal("SESSDATA=test-session", request.Cookie);
        Assert.False(request.Query.ContainsKey("access_key"));
    }

    [Fact]
    public async Task PlayerMetadataUsesWebWbiWithoutTheAppToken()
    {
        using var handler = new RecordingHttpMessageHandler().With("/x/web-interface/nav",
            """{"code":0,"data":{"wbi_img":{"img_url":"https://example.com/7cd084941338484aae1ad9425b84077c.png","sub_url":"https://example.com/4932caff0ff746eab6f01bf08b70ac45.png"}}}""");
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "test-session" });
        var tokens = new FakeTokenResolver { IsAndroidHdToken = true, Token = new BiliToken { AccessToken = "app-token" } };
        var authenticator = new BiliAuthenticator(cookies, tokens, httpClient: http);

        using var request = await authenticator.CreateRestRequestAsync(HttpMethod.Get,
            new Uri("https://api.bilibili.com/x/player/wbi/v2"), new() { ["aid"] = "123", ["cid"] = "456" });

        Assert.Single(handler.Requests);
        Assert.Contains("w_rid=", request.RequestUri!.Query);
        Assert.Contains("aid=123", request.RequestUri.Query);
        Assert.DoesNotContain("access_key=", request.RequestUri.Query);
        Assert.Equal("SESSDATA=test-session", request.Headers.GetValues("Cookie").Single());
    }

    [Fact]
    public async Task EmptyGrpcAuthenticationRejectionRetainsItsStatus()
    {
        using var handler = new RecordingHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
            response.Content.Headers.ContentType = new("application/grpc");
            response.TrailingHeaders.Add("grpc-status", "16");
            return response;
        });
        using var http = new BiliHttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://grpc.biliapi.net/test");

        var error = await Assert.ThrowsAsync<KernelException>(() => http.SendAsync(request));

        Assert.Contains("gRPC", error.Message);
        Assert.Contains("16", error.Message);
    }

    [Theory]
    [InlineData("api.bilibili.com", true)]
    [InlineData("example.invalid", false)]
    public void WebMessagePolicyAppliesOnlyToItsRegisteredEndpoint(string host, bool shouldUseWebSession)
    {
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "test-session" });
        var auth = new BiliAuthenticator(cookies, new FakeTokenResolver());
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}/x/msgfeed/like");

        auth.AuthorizeRestRequest(request, new Dictionary<string, string> { ["access_key"] = "caller-app-token" });

        Assert.Equal(shouldUseWebSession, request.Headers.Contains("Cookie"));
        Assert.Equal(shouldUseWebSession, request.RequestUri!.Query.Contains("mobi_app=web", StringComparison.Ordinal));
        if (shouldUseWebSession)
        {
            Assert.DoesNotContain("access_key=", request.RequestUri.Query);
        }
    }

    [Theory]
    [InlineData(NotifyMessageType.Like, "like")]
    [InlineData(NotifyMessageType.Reply, "reply")]
    [InlineData(NotifyMessageType.At, "at")]
    public async Task EmptyNotificationsUseWebCredentialsAndReturnAnEmptyPage(NotifyMessageType type, string endpoint)
    {
        using var handler = new RecordingHttpMessageHandler().With("/x/msgfeed/" + endpoint, """{"code":0,"data":{"items":[]}}""");
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "test-session" });
        var tokens = new FakeTokenResolver { Token = new BiliToken { AccessToken = "app-token" } };
        var service = new MessageService(http, new FakeAuthenticationService(), tokens, new BiliAuthenticator(cookies, tokens));

        var (messages, _, _, hasMore) = await service.GetNotifyMessagesAsync(type, 42, 50);

        Assert.Empty(messages);
        Assert.False(hasMore);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("SESSDATA=test-session", request.Cookie);
        Assert.Equal("web", request.Query["platform"]);
        Assert.Equal("42", request.Query["id"]);
        Assert.Equal("50", request.Query[endpoint + "_time"]);
        Assert.False(request.Query.ContainsKey("access_key"));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""[{"talker_id":123,"system_msg_type":1}]""")]
    public async Task ChatWithoutDisplayableSessionsSkipsTheUserLookup(string sessionList)
    {
        using var handler = new RecordingHttpMessageHandler()
            .With("/x/web-interface/nav", """{"code":0,"data":{"wbi_img":{"img_url":"https://example.com/7cd084941338484aae1ad9425b84077c.png","sub_url":"https://example.com/4932caff0ff746eab6f01bf08b70ac45.png"}}}""")
            .With("/session_svr/v1/session_svr/get_sessions", $$$"""{"code":0,"data":{"session_list":{{{sessionList}}},"has_more":0}}""");
        using var http = new BiliHttpClient(handler);
        var tokens = new FakeTokenResolver();
        var service = new MessageService(http, new FakeAuthenticationService(), tokens, new BiliAuthenticator(new FakeCookiesResolver(), tokens, httpClient: http));

        var (sessions, _, hasMore) = await service.GetChatSessionsAsync();

        Assert.Empty(sessions);
        Assert.False(hasMore);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task VideoPartitionDirectoryDoesNotSendTheCurrentAccountsCredentials()
    {
        using var handler = new RecordingHttpMessageHandler().With("/x/v2/region/index",
            """{"code":0,"data":[{"tid":1,"name":"鍔ㄧ敾","uri":"bilibili://region/1","children":[{"tid":24,"name":"MAD"}]}]}""");
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "test-session" });
        var tokens = new FakeTokenResolver { Token = new BiliToken { AccessToken = "app-token" } };
        var service = new VideoDiscoveryService(http, new BiliAuthenticator(cookies, tokens), tokens);

        var partitions = await service.GetVideoPartitionsAsync(CancellationToken.None);

        Assert.Equal("1", Assert.Single(partitions).Id);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/x/v2/region/index", request.Path);
        Assert.True(request.Query.ContainsKey("sign"));
        Assert.False(request.Query.ContainsKey("access_key"));
        Assert.Null(request.Cookie);
    }

    [Fact]
    public async Task AnEmptyDefaultFavoriteFolderStillLoadsTheFavoritesPageData()
    {
        using var handler = new RecordingHttpMessageHandler()
            .With("/x/v3/fav/folder/space/v2",
                """{"code":0,"data":{"space_infos":[{"id":1,"name":"Created"},{"id":2,"name":"Collected"}],"default_folder":{"folder_detail":{"id":123,"title":"Default"},"medias":null}}}""")
            .With("/x/v3/fav/folder/created/list-all",
                """{"code":0,"data":{"count":2,"list":[{"id":123,"title":"Default"},{"id":124,"title":"Another folder"}]}}""")
            .With("/x/v3/fav/folder/collected/list", """{"code":0,"data":{"count":0,"list":null}}""");
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "test-session" });
        var tokens = new FakeTokenResolver { IsAndroidHdToken = true, Token = new BiliToken { UserId = 42, AccessToken = "app-token" } };
        var service = new FavoriteService(http, new FakeAuthenticationService(), tokens, new BiliAuthenticator(cookies, tokens));

        var (groups, defaultFolder) = await service.GetVideoFavoriteGroupsAsync();

        Assert.Equal("123", defaultFolder.Folder.Id);
        Assert.Empty(defaultFolder.Videos!);
        Assert.Equal("124", Assert.Single(groups[0].Folders).Id);
        Assert.Empty(groups[1].Folders);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("SESSDATA=test-session", request.Cookie);
            Assert.False(request.Query.ContainsKey("access_key"));
        });
    }

    [Theory]
    [InlineData(PgcFavoriteType.Anime, "1", false)]
    [InlineData(PgcFavoriteType.Cinema, "2", true)]
    public async Task PgcFavoritesUseTheAccountsWebSession(PgcFavoriteType type, string requestType, bool androidHd)
    {
        using var handler = new RecordingHttpMessageHandler().With("/x/space/bangumi/follow/list",
            """{"code":0,"data":{"total":1,"list":[{"season_id":123,"title":"Test","cover":"https://example.com/cover.png","progress":"看到第5话"}]}}""");
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "test-session" });
        var tokens = new FakeTokenResolver { IsAndroidHdToken = androidHd, Token = new BiliToken { UserId = 42, AccessToken = "app-token" } };
        var service = new FavoriteService(http, new FakeAuthenticationService(), tokens, new BiliAuthenticator(cookies, tokens));

        var (seasons, count, next) = await service.GetPgcFavoritesAsync(type, PgcFavoriteStatus.Watching);

        Assert.Single(seasons);
        Assert.Equal(1, count);
        Assert.Null(next);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("SESSDATA=test-session", request.Cookie);
        Assert.Equal("42", request.Query["vmid"]);
        Assert.Equal(requestType, request.Query["type"]);
        Assert.Equal("2", request.Query["follow_status"]);
        Assert.False(request.Query.ContainsKey("access_key"));
        Assert.False(request.Query.ContainsKey("sign"));
    }

    [Fact]
    public async Task UnreadMessageCountsUseTheCurrentAccountsWebSession()
    {
        using var handler = new RecordingHttpMessageHandler().With("/x/msgfeed/unread",
            """{"code":0,"data":{"at":1,"reply":2,"like":3,"chat":4}}""");
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "test-session" });
        var tokens = new FakeTokenResolver { Token = new BiliToken { AccessToken = "app-token" } };
        var service = new MessageService(http, new FakeAuthenticationService(), tokens, new BiliAuthenticator(cookies, tokens));

        var unread = await service.GetUnreadInformationAsync();

        Assert.Equal(10, unread.Total);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/x/msgfeed/unread", request.Path);
        Assert.Equal("SESSDATA=test-session", request.Cookie);
        Assert.Equal("web", request.Query["mobi_app"]);
        Assert.False(request.Query.ContainsKey("access_key"));
        Assert.False(request.Query.ContainsKey("appkey"));
    }
}
