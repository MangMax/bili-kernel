// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Xunit;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Moment;
using Richasy.BiliKernel.Models.Article;
using RichasyKernel;
using System.Net;
using System.Text;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Services.Moment;

namespace Richasy.BiliKernel.Tests;

public sealed class WebMomentTests
{
    [Fact]
    public async Task EmptyPageUsesOnlyCookieAndPreservesOpaqueCursor()
    {
        using var handler = new Handler("""{"code":0,"data":{"items":[],"offset":"next/+?","has_more":false}}""");
        using var client = new BiliHttpClient(handler);
        var service = new WebMomentDiscoveryService(client, new BiliAuthenticator(new Cookies(), new Token(), httpClient: client));
        var result = await service.GetComprehensiveMomentsAsync("input/+?");
        Assert.Empty(result.Moments);
        Assert.Equal("next/+?", result.Offset);
        Assert.False(result.HasMoreMoments);
        Assert.Null(result.UpdateBaseline);
        Assert.Contains("offset=input%2F%2B%3F", handler.ListQuery);
        Assert.DoesNotContain("access_key", handler.ListQuery);
        Assert.Contains("SESSDATA=web-test-session", handler.Cookie);
    }

    private static string Item(string type, string major, string id = "900", string extra = "") =>
        "{\"id_str\":\"" + id + "\",\"type\":\"" + type + "\",\"modules\":{\"module_author\":{\"mid\":42,\"name\":\"author\",\"pub_ts\":1700000000},\"module_dynamic\":{\"desc\":{\"text\":\"body\\n[smile]\",\"rich_text_nodes\":[{\"type\":\"RICH_TEXT_NODE_TYPE_EMOJI\",\"text\":\"[smile]\",\"emoji\":{\"icon_url\":\"https://example.com/e.png\"}}]},\"major\":" + major + "},\"module_stat\":{\"like\":{\"count\":7,\"status\":true},\"comment\":{\"count\":3}}}" + extra + "}";
    private static string Page(string items) => "{\"code\":0,\"data\":{\"items\":[" + items + "],\"offset\":\"cursor\",\"has_more\":true}}";

    [Theory]
    [InlineData("description")]
    [InlineData("")]
    [InlineData("-")]
    public async Task VideoAndForwardKeepListDescriptionAndIndependentOperationTargets(string description)
    {
        var original = Item("DYNAMIC_TYPE_AV", "{\"archive\":{\"aid\":123,\"bvid\":\"BVtest\",\"title\":\"联合投稿 充电 动态视频\",\"desc\":\"" + description + "\",\"cover\":\"https://example.com/c.png\",\"duration_text\":\"01:02\",\"stat\":{\"play\":\"1.2万\",\"danmaku\":8},\"rights\":{\"is_cooperation\":1},\"is_upower_exclusive\":true}}", "901");
        using var handler = new Handler(Page(original + "," + Item("DYNAMIC_TYPE_FORWARD", "null", extra: ",\"orig\":" + original)));
        using var client = new BiliHttpClient(handler);
        var service = new WebMomentDiscoveryService(client, new BiliAuthenticator(new Cookies(), new Token(), httpClient: client));
        var page = await service.GetComprehensiveMomentsAsync();
        Assert.Equal(2, page.Moments.Count);
        var direct = page.Moments[0];
        var forward = page.Moments[1];
        Assert.Equal("900", forward.CommentId);
        Assert.Equal(CommentTargetType.Moment, forward.CommentType);
        var nested = Assert.IsType<MomentInformation>(forward.Data);
        Assert.Equal("901", nested.Id);
        foreach (var moment in new[] { direct, nested })
        {
            Assert.Equal("123", moment.CommentId);
            Assert.Equal(CommentTargetType.Video, moment.CommentType);
            Assert.Equal("body\n[smile]", moment.Description!.Text);
            Assert.Single(moment.Description.Emotes!);
            Assert.Equal(7, moment.CommunityInformation!.LikeCount);
            Assert.True(moment.CommunityInformation.IsLiked);
            var video = Assert.IsType<VideoInformation>(moment.Data);
            Assert.Equal(description, video.GetExtensionIfNotNull<string>(VideoExtensionDataId.Description));
            Assert.Equal("123", video.Identifier.Id);
            Assert.Equal("BVtest", video.BvId);
            Assert.Equal(62, video.Duration);
            Assert.Equal(12000d, video.CommunityInformation!.PlayCount);
            Assert.Equal("42", video.Publisher.User.Id);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), video.PublishTime);
            Assert.True(video.GetExtensionIfNotNull<bool>(VideoExtensionDataId.IsCollaborativeSubmission));
            Assert.True(video.GetExtensionIfNotNull<bool>(VideoExtensionDataId.IsChargingExclusive));
        }
        Assert.Equal(1, handler.Requests);
    }

    [Theory]
    [InlineData("{\"data\":{\"items\":[],\"has_more\":false}}") ]
    [InlineData("{\"code\":-101,\"message\":\"expired\"}")]
    [InlineData("{\"code\":-352,\"message\":\"risk\"}")]
    [InlineData("{\"code\":0,\"data\":null}")]
    [InlineData("{\"code\":0,\"data\":{\"items\":[]}}")]
    [InlineData("{\"code\":0,\"data\":{\"items\":[],\"has_more\":true}}")]
    public async Task InvalidPagesThrowInsteadOfBecomingEmpty(string json)
    {
        using var handler = new Handler(json);
        using var client = new BiliHttpClient(handler);
        var service = new WebMomentDiscoveryService(client, new BiliAuthenticator(new Cookies(), new Token(), httpClient: client));
        await Assert.ThrowsAsync<KernelException>(() => service.GetComprehensiveMomentsAsync());
    }

    [Fact]
    public async Task TextImagesOpusAndUnknownTypesRemainVisible()
    {
        var items = Item("DYNAMIC_TYPE_WORD", "null") + "," + Item("DYNAMIC_TYPE_DRAW", "{\"draw\":{\"id\":88,\"items\":[{\"src\":\"https://example.com/p.png\"}]}}") + "," +
            Item("DYNAMIC_TYPE_ARTICLE", "{\"opus\":{\"opus_id\":\"77\",\"title\":\"title\",\"summary\":{\"text\":\"summary\"}}}") + "," + Item("DYNAMIC_TYPE_UNKNOWN", "null");
        using var handler = new Handler(Page(items));
        using var client = new BiliHttpClient(handler);
        var service = new WebMomentDiscoveryService(client, new BiliAuthenticator(new Cookies(), new Token(), httpClient: client));
        var page = await service.GetComprehensiveMomentsAsync();
        Assert.Equal(MomentItemType.PlainText, page.Moments[0].MomentType);
        Assert.Equal(MomentItemType.Image, page.Moments[1].MomentType);
        Assert.Equal("88", page.Moments[1].CommentId);
        Assert.Single(Assert.IsType<List<Richasy.BiliKernel.Models.Appearance.BiliImage>>(page.Moments[1].Data));
        var article = Assert.IsType<ArticleInformation>(page.Moments[2].Data);
        Assert.Equal("77", article.Identifier.Id);
        Assert.Equal("title", article.Identifier.Title);
        Assert.Equal("summary", article.Identifier.Summary);
        Assert.Equal("body\n[smile]", page.Moments[2].Description!.Text);
        Assert.Equal(MomentItemType.Unsupported, page.Moments[3].MomentType);
    }

    [Fact]
    public async Task CancellationPropagates()
    {
        using var handler = new Handler(Page(""));
        using var client = new BiliHttpClient(handler);
        var service = new WebMomentDiscoveryService(client, new BiliAuthenticator(new Cookies(), new Token(), httpClient: client));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetComprehensiveMomentsAsync(cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task OpusKeepsPicturesAndUsesExplicitCommentTarget()
    {
        var item = Item("DYNAMIC_TYPE_DRAW", "{\"opus\":{\"opus_id\":\"77\",\"title\":\"title\",\"summary\":{\"text\":\"summary\"},\"pics\":[{\"url\":\"https://example.com/opus.png\"}]}}", extra: ",\"basic\":{\"comment_id_str\":\"555\",\"comment_type\":11}");
        using var handler = new Handler(Page(item));
        using var client = new BiliHttpClient(handler);
        var service = new WebMomentDiscoveryService(client, new BiliAuthenticator(new Cookies(), new Token(), httpClient: client));
        var moment = Assert.Single((await service.GetComprehensiveMomentsAsync()).Moments);
        Assert.Equal("555", moment.CommentId);
        Assert.Equal(CommentTargetType.Album, moment.CommentType);
        var article = Assert.IsType<ArticleInformation>(moment.Data);
        Assert.NotNull(article.Identifier.Cover);
        Assert.Single(article.GetExtensionIfNotNull<List<Richasy.BiliKernel.Models.Appearance.BiliImage>>(ArticleExtensionDataId.Pictures)!);
    }

    [Fact]
    public async Task PgcKeepsEpisodeAndVideoTargetsSeparate()
    {
        using var handler = new Handler(Page(Item("DYNAMIC_TYPE_PGC", "{\"pgc\":{\"epid\":22,\"aid\":33,\"season_id\":44,\"title\":\"episode\",\"cover\":\"https://example.com/e.png\"}}")));
        using var client = new BiliHttpClient(handler);
        var service = new WebMomentDiscoveryService(client, new BiliAuthenticator(new Cookies(), new Token(), httpClient: client));
        var moment = Assert.Single((await service.GetComprehensiveMomentsAsync()).Moments);
        Assert.Equal(MomentItemType.Pgc, moment.MomentType);
        Assert.Equal("33", moment.CommentId);
        var episode = Assert.IsType<EpisodeInformation>(moment.Data);
        Assert.Equal("22", episode.Identifier.Id);
        Assert.Equal(44, episode.GetExtensionIfNotNull<long>(EpisodeExtensionDataId.SeasonId));
    }

    [Fact]
    public async Task TitlesDoNotCreateContentFlags()
    {
        using var handler = new Handler(Page(Item("DYNAMIC_TYPE_AV", "{\"archive\":{\"aid\":123,\"title\":\"联合投稿 充电 动态视频\",\"desc\":\"\"}}")));
        using var client = new BiliHttpClient(handler);
        var service = new WebMomentDiscoveryService(client, new BiliAuthenticator(new Cookies(), new Token(), httpClient: client));
        var video = Assert.IsType<VideoInformation>(Assert.Single((await service.GetComprehensiveMomentsAsync()).Moments).Data);
        Assert.False(video.ExtensionData!.ContainsKey(VideoExtensionDataId.IsCollaborativeSubmission));
        Assert.False(video.ExtensionData.ContainsKey(VideoExtensionDataId.IsChargingExclusive));
    }

    [Fact]
    public void WebRegistrationLeavesAppRegistrationAvailable()
    {
        var builder = RichasyKernel.Kernel.CreateBuilder();
        builder.AddMomentDiscoveryService();
        builder.AddWebMomentDiscoveryService();
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(Richasy.BiliKernel.Bili.Moment.IMomentDiscoveryService));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(Richasy.BiliKernel.Bili.Moment.IWebMomentDiscoveryService));
    }

    private sealed class Token : IBiliTokenResolver
    {
        public BiliToken GetToken() => new() { AccessToken = "app-token-must-not-be-sent", UserId = 1000 };
        public void SaveToken(BiliToken token) => throw new NotSupportedException();
        public void RemoveToken() => throw new NotSupportedException();
    }
    private sealed class Cookies : IBiliCookiesResolver
    {
        public IDictionary<string, string> GetCookies() => new Dictionary<string, string> { ["SESSDATA"] = "web-test-session" };
        public string GetCookieString() => "SESSDATA=web-test-session";
        public void SaveCookies(IDictionary<string, string> cookies) => throw new NotSupportedException();
        public void RemoveCookies() => throw new NotSupportedException();
    }
    private sealed class NoTokenRefresh : IAuthenticationService
    {
        public Task EnsureTokenAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("A cookie-only read must not refresh the App token.");
        public Task SignInAsync(AuthorizeExecutionSettings? settings = default, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SignOutAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Handler(string response) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public string ListQuery { get; private set; } = "";
        public string Cookie { get; private set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = """{"code":0,"data":{"wbi_img":{"img_url":"https://example.com/7cd084941338484aae1ad9425b84077c.png","sub_url":"https://example.com/4932caff0ff746eab6f01bf08b70ac45.png"}}}""";
            if (request.RequestUri!.AbsolutePath == "/x/polymer/web-dynamic/v1/feed/all")
            {
                Requests++;
                Assert.Null(request.Headers.Authorization);
                Assert.DoesNotContain("app-token-must-not-be-sent", request.ToString());
                ListQuery = request.RequestUri.Query;
                Cookie = string.Join(";", request.Headers.GetValues("Cookie"));
                json = response;
            }
            else Assert.Equal("/x/web-interface/nav", request.RequestUri.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}

