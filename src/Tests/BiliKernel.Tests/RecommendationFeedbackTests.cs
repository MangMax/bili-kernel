// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media;
using Xunit;

namespace Richasy.BiliKernel.Tests;

public sealed class RecommendationFeedbackTests
{
    internal const string AppFeed = """
        {"code":0,"data":{"items":[{"card_goto":"av","goto":"av","param":"123","idx":9,"title":"Video","cover":"https://example.com/c.jpg","args":{"aid":123,"up_id":42,"up_name":"author"},"cover_left_text_1":"100","cover_left_text_2":"10","cover_right_text":"01:00","three_point_v2":[{"type":"dislike","title":"changed","reasons":[{"id":1,"name":"changed"},{"id":4,"mid":42,"name":"changed"},{"id":4,"mid":43,"rid":36,"tag_id":12,"name":"changed"}]},{"type":"feedback","reasons":[{"id":1,"name":"other"}]}]}]}}
        """;

    [Fact]
    public async Task AppReadToSubmitPreservesDuplicateTargetsAndUsesSignedAppIdentity()
    {
        using var handler = new RecordingHttpMessageHandler().With("/x/v2/feed/index", AppFeed);
        using var http = new BiliHttpClient(handler);
        var tokens = new FakeTokenResolver { Token = new() { AccessToken = "app-token" } };
        var service = new VideoDiscoveryService(http, new BiliAuthenticator(null, tokens), tokens);
        var (items, _) = await service.GetRecommendVideoListAsync();
        var context = Assert.Single(items).RecommendationFeedback!;
        Assert.Equal(RecommendationFeedbackSource.App, context.Source);
        Assert.Equal(4, context.Reasons.Count);
        Assert.Null(context.Reasons[3].Operation);
        Assert.Equal(3, context.Options.Count);
        foreach (var option in context.Options)
            await service.SubmitRecommendationFeedbackAsync(context, option);
        var requests = handler.Requests.Skip(1).ToArray();
        Assert.Equal(3, requests.Length);
        Assert.All(requests, r => {
            Assert.Equal("/x/feed/dislike", r.Path);
            Assert.Equal("GET", r.Method);
            Assert.Equal("123", r.Query["id"]);
            Assert.Equal("app-token", r.Query["access_key"]);
            Assert.True(r.Query.ContainsKey("sign"));
            Assert.Null(r.Cookie);
            Assert.False(r.Query.ContainsKey("feedback_id"));
        });
        Assert.Equal("1", requests[0].Query["reason_id"]);
        Assert.Equal("42", requests[1].Query["mid"]);
        Assert.Equal("43", requests[2].Query["mid"]);
        Assert.Equal("36", requests[2].Query["rid"]);
        Assert.Equal("12", requests[2].Query["tag_id"]);
    }

    private const string WebFeed = """
        {"code":0,"data":{"item":[{"id":123,"goto":"av","track_id":"track-123","title":"Video","pic":"https://example.com/c.jpg","owner":{"mid":42,"name":"author"},"stat":{"view":100,"danmaku":10}}]}}
        """;

    private static (RecordingHttpMessageHandler Handler, VideoDiscoveryService Service) Create(bool web, bool cookie = true, bool token = true, string? appFeed = null)
    {
        var handler = new RecordingHttpMessageHandler()
            .With("/x/v2/feed/index", appFeed ?? AppFeed)
            .With("/feed/rcmd", WebFeed)
            .With("/x/web-interface/nav", """{"code":0,"data":{"wbi_img":{"img_url":"https://example.com/abcdefghijklmnopqrstuvwxyz123456.png","sub_url":"https://example.com/abcdefghijklmnopqrstuvwxyz123456.png"}}}""");
        var cookies = new FakeCookiesResolver();
        if (cookie) cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session", ["bili_jct"] = "csrf-token" });
        var tokens = new FakeTokenResolver { Token = token ? new() { AccessToken = "app-token" } : null };
        var http = new BiliHttpClient(handler);
        return (handler, new VideoDiscoveryService(http, new BiliAuthenticator(cookies, tokens, httpClient: http), tokens));
    }

    private static async Task<RecommendationFeedbackContext> Read(VideoDiscoveryService service, bool web)
    {
        var (items, _) = web ? await service.GetCuratedFeedAsync().ConfigureAwait(false) : await service.GetRecommendVideoListAsync().ConfigureAwait(false);
        return Assert.Single(items).RecommendationFeedback!;
    }

    [Fact]
    public async Task WebReadToSubmitUsesCookieCsrfAndFixedReasonsWithTracking()
    {
        var (handler, service) = Create(true);
        using (handler)
        {
            var context = await Read(service, true);
            Assert.Equal(RecommendationFeedbackSource.Web, context.Source);
            Assert.Equal(2, context.Options.Count);
            foreach (var reason in context.Options) await service.SubmitRecommendationFeedbackAsync(context, reason);
            var writes = handler.Requests.Where(r => r.Path.EndsWith("/dislike", StringComparison.Ordinal)).ToList();
            Assert.Equal(2, writes.Count);
            Assert.Contains("reason_id=1", writes[0].Body);
            Assert.Contains("reason_id=2", writes[1].Body);
            Assert.All(writes, r => {
                Assert.Equal("/x/web-interface/feedback/dislike", r.Path);
                Assert.Equal("POST", r.Method);
                Assert.Contains("application/x-www-form-urlencoded", r.Headers["Content-Type"]);
                Assert.Contains("id=123", r.Body);
                Assert.Contains("mid=42", r.Body);
                Assert.Contains("track_id=track-123", r.Body);
                Assert.Contains("platform=5", r.Body);
                Assert.Contains("csrf=csrf-token", r.Body);
                Assert.Contains("SESSDATA=session", r.Cookie);
                Assert.DoesNotContain("access_key", r.Body);
                Assert.False(r.Headers.ContainsKey("authorization"));
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingIdentityRejectsBeforeSubmission(bool web)
    {
        var (handler, service) = Create(web, cookie: false, token: false);
        using (handler)
        {
            var context = await Read(service, web);
            var count = handler.Requests.Count;
            await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => service.SubmitRecommendationFeedbackAsync(context, context.Options[0]));
            Assert.Equal(count, handler.Requests.Count);
        }
    }

    [Theory]
    [InlineData(false, "{\"code\":-101}")]
    [InlineData(true, "{\"code\":-101}")]
    [InlineData(false, "{}")]
    [InlineData(true, "{}")]
    [InlineData(false, "null")]
    [InlineData(true, "invalid")]
    public async Task InvalidOrRejectedResponsesNeverSucceedOrRetry(bool web, string response)
    {
        var (handler, service) = Create(web);
        using (handler)
        {
            var context = await Read(service, web);
            handler.With("/dislike", response);
            await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => service.SubmitRecommendationFeedbackAsync(context, context.Options[0]));
            Assert.Single(handler.Requests, r => r.Path.EndsWith("/dislike", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDoesNotSubmit(bool web)
    {
        var (handler, service) = Create(web);
        using (handler)
        {
            var context = await Read(service, web);
            var count = handler.Requests.Count;
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SubmitRecommendationFeedbackAsync(context, context.Options[0], cancellation.Token));
            Assert.Equal(count, handler.Requests.Count);
        }
    }

    [Fact]
    public async Task MissingReasonsAndForeignOptionsAreUnsupported()
    {
        var (handler, service) = Create(false, appFeed: AppFeed.Replace("\"type\":\"dislike\"", "\"type\":\"unknown\""));
        using (handler)
        {
            var context = await Read(service, false);
            Assert.Empty(context.Options);
            await Assert.ThrowsAsync<NotSupportedException>(() => service.SubmitRecommendationFeedbackAsync(context, context.Reasons[0]));
            var foreign = new RecommendationFeedbackReason { Id = 1, Operation = RecommendationFeedbackOperation.NotInterested };
            await Assert.ThrowsAsync<NotSupportedException>(() => service.SubmitRecommendationFeedbackAsync(context, foreign));
            Assert.Single(handler.Requests);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task TransportFailurePropagatesWithoutRetry(bool web, bool httpFailure)
    {
        var reader = Create(web);
        using var readHandler = reader.Handler;
        var context = await Read(reader.Service, web);
        using var handler = new RecordingHttpMessageHandler(_ => httpFailure
            ? RecordingHttpMessageHandler.Json("{\"code\":0}", System.Net.HttpStatusCode.InternalServerError)
            : throw new HttpRequestException("offline failure"));
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session", ["bili_jct"] = "csrf-token" });
        var tokens = new FakeTokenResolver { Token = new() { AccessToken = "app-token" } };
        var service = new VideoDiscoveryService(http, new BiliAuthenticator(cookies, tokens), tokens);
        await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => service.SubmitRecommendationFeedbackAsync(context, context.Options[0]));
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingTargetsExposeNoCapability(bool web)
    {
        var (handler, service) = Create(web);
        using (handler)
        {
            var context = await Read(service, web);
            context.VideoId = "0";
            Assert.Empty(context.Options);
            await Assert.ThrowsAsync<NotSupportedException>(() => service.SubmitRecommendationFeedbackAsync(context, context.Reasons[0]));
            context.VideoId = "123";
            context.PublisherId = null;
            foreach (var reason in context.Reasons) reason.Mid = null;
            Assert.DoesNotContain(context.Options, r => r.Operation == RecommendationFeedbackOperation.AvoidPublisher);
        }
    }

    [Fact]
    public async Task WebReadAndSubmissionNeverReadAppToken()
    {
        var reader = Create(true);
        using var handler = reader.Handler;
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session", ["bili_jct"] = "csrf-token" });
        var tokens = new ForbiddenTokenResolver();
        var service = new VideoDiscoveryService(http, new BiliAuthenticator(cookies, tokens, httpClient: http), tokens);
        var context = await Read(service, true);
        await service.SubmitRecommendationFeedbackAsync(context, context.Options[0]);
        Assert.Single(handler.Requests, r => r.Path.EndsWith("/dislike", StringComparison.Ordinal));
    }

    private sealed class ForbiddenTokenResolver : Richasy.BiliKernel.Bili.Authorization.IBiliTokenResolver
    {
        public Richasy.BiliKernel.Models.Authorization.BiliToken? GetToken() => throw new InvalidOperationException("Web must not read App credentials");
        public void SaveToken(Richasy.BiliKernel.Models.Authorization.BiliToken token) => throw new InvalidOperationException();
        public void RemoveToken() => throw new InvalidOperationException();
    }

    [Fact]
    public async Task AppAbsentReasonTargetUsesSameCardAuthorAndMissingMenuStaysUnsupported()
    {
        var (handler, service) = Create(false, appFeed: AppFeed.Replace("\"mid\":42,", string.Empty));
        using (handler)
        {
            var context = await Read(service, false);
            await service.SubmitRecommendationFeedbackAsync(context, context.Options[1]);
            Assert.Equal("42", handler.Requests.Last().Query["mid"]);
        }
        var missing = Create(false, appFeed: AppFeed.Replace("three_point_v2", "absent_menu"));
        using (missing.Handler)
        {
            var context = await Read(missing.Service, false);
            Assert.Empty(context.Reasons);
            Assert.Empty(context.Options);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InFlightCancellationPropagatesWithoutRetry(bool web)
    {
        var reader = Create(web);
        using var readHandler = reader.Handler;
        var context = await Read(reader.Service, web);
        using var cancellation = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler(_ => { cancellation.Cancel(); cancellation.Token.ThrowIfCancellationRequested(); return RecordingHttpMessageHandler.Json("{\"code\":0}"); });
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session", ["bili_jct"] = "csrf-token" });
        var tokens = new FakeTokenResolver { Token = new() { AccessToken = "app-token" } };
        var service = new VideoDiscoveryService(http, new BiliAuthenticator(cookies, tokens), tokens);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SubmitRecommendationFeedbackAsync(context, context.Options[0], cancellation.Token));
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("SESSDATA")]
    [InlineData("bili_jct")]
    public async Task PartialWebIdentityRejectsBeforeSending(string key)
    {
        var reader = Create(true);
        using var readHandler = reader.Handler;
        var context = await Read(reader.Service, true);
        using var handler = new RecordingHttpMessageHandler();
        using var http = new BiliHttpClient(handler);
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { [key] = "value" });
        var service = new VideoDiscoveryService(http, new BiliAuthenticator(cookies, new ForbiddenTokenResolver()), new ForbiddenTokenResolver());
        await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => service.SubmitRecommendationFeedbackAsync(context, context.Options[0]));
        Assert.Empty(handler.Requests);
    }
}
