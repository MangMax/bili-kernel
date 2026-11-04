using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Xunit;

namespace Richasy.BiliKernel.Tests;

public sealed class WbiLifecycleTests
{
    internal const string Nav = "{\"code\":0,\"data\":{\"wbi_img\":{\"img_url\":\"https://example.test/0123456789abcdef0123456789abcdef.png\",\"sub_url\":\"https://example.test/fedcba9876543210fedcba9876543210.png\"}}}";

    [Fact]
    public async Task WbiReusesUntilExactlyTwentyFourHoursThenRefreshes()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        using var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(Nav));
        using var client = new BiliHttpClient(handler);
        var auth = new BiliAuthenticator(null, null, null, client, () => now);
        await auth.InitializeWbiAsync();
        now = now.AddHours(24).AddTicks(-1);
        await auth.InitializeWbiAsync();
        Assert.Single(handler.Requests);
        now = now.AddTicks(1);
        await auth.InitializeWbiAsync();
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ConcurrentWbiInitializationSharesOneRequest()
    {
        using var handler = new GatedHandler();
        using var client = new BiliHttpClient(handler);
        var auth = new BiliAuthenticator(null, null, null, client);
        var first = auth.InitializeWbiAsync();
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var others = Enumerable.Range(0, 8).Select(_ => auth.InitializeWbiAsync()).ToArray();
        handler.Release.TrySetResult();
        await Task.WhenAll(others.Append(first));
        Assert.Equal(1, handler.Count);
    }


    [Theory]
    [InlineData("{}")]
    [InlineData("{\"code\":0,\"data\":{\"wbi_img\":{\"img_url\":\"a.png\",\"sub_url\":\"b.png\"}}}")]
    public async Task ExpiredWbiFailureRejectsSigningAndNextExplicitRequestRecovers(string invalid)
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var response = Nav;
        using var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(response));
        using var client = new BiliHttpClient(handler);
        var auth = new BiliAuthenticator(null, null, null, client, () => now);
        Assert.Throws<InvalidOperationException>(() => Sign(auth));
        await auth.InitializeWbiAsync();
        var original = Sign(auth);
        now = now.AddHours(24);
        response = invalid;
        await Assert.ThrowsAsync<InvalidOperationException>(() => auth.InitializeWbiAsync());
        Assert.Equal(2, handler.Requests.Count);
        Assert.Throws<InvalidOperationException>(() => Sign(auth));
        response = Nav.Replace("0123456789abcdef", "aaaaaaaaaaaaaaaa", StringComparison.Ordinal);
        await auth.InitializeWbiAsync();
        Assert.NotEqual(original, Sign(auth));
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task WbiOwnerCancellationAllowsNextExplicitRequestToRecover()
    {
        using var handler = new GatedHandler();
        using var client = new BiliHttpClient(handler);
        var auth = new BiliAuthenticator(null, null, null, client);
        using var cancellation = new CancellationTokenSource();
        var first = auth.InitializeWbiAsync(cancellation.Token);
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Throws<InvalidOperationException>(() => Sign(auth));
        handler.Release.TrySetResult();
        await auth.InitializeWbiAsync();
        Assert.NotEmpty(Sign(auth));
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task CancelledWbiWaiterDoesNotCancelSharedRefresh()
    {
        using var handler = new GatedHandler();
        using var client = new BiliHttpClient(handler);
        var auth = new BiliAuthenticator(null, null, null, client);
        var owner = auth.InitializeWbiAsync();
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource();
        var waiter = auth.InitializeWbiAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        Assert.False(owner.IsCompleted);
        handler.Release.TrySetResult();
        await owner.ConfigureAwait(true);
        await auth.InitializeWbiAsync();
        Assert.Equal(1, handler.Count);
    }


    [Fact]
    public async Task ColdPlayerChaptersInitializesWbiBeforeSigning()
    {
        using var handler = new RecordingHttpMessageHandler().With("/x/web-interface/nav", Nav)
            .With("/x/player/wbi/v2", "{\"code\":0,\"data\":{\"view_points\":[]}}");
        using var client = new BiliHttpClient(handler);
        var auth = new BiliAuthenticator(null, null, null, client);
        var service = new Richasy.BiliKernel.Services.Media.PlayerService(client, auth, new FakeTokenResolver());
        var chapters = await service.GetVideoChaptersAsync(new Richasy.BiliKernel.Models.Media.MediaIdentifier("123", null, null), 456);
        Assert.Empty(chapters);
        Assert.Equal("/x/web-interface/nav", handler.Requests[0].Path);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Matches("^[0-9a-f]{32}$", handler.Requests[1].Query["w_rid"]);
    }

    [Fact]
    public async Task ColdChatSessionsInitializesWbiBeforeSigning()
    {
        using var handler = new RecordingHttpMessageHandler().With("/x/web-interface/nav", Nav)
            .With("/session_svr/v1/session_svr/get_sessions", "{\"code\":0,\"data\":{}}");
        using var client = new BiliHttpClient(handler);
        var auth = new BiliAuthenticator(null, null, null, client);
        var service = new Richasy.BiliKernel.Services.User.MessageService(client, new FakeAuthenticationService(), new FakeTokenResolver(), auth);
        var result = await service.GetChatSessionsAsync();
        Assert.Empty(result.Sessions);
        Assert.Equal("/x/web-interface/nav", handler.Requests[0].Path);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Matches("^[0-9a-f]{32}$", handler.Requests[1].Query["w_rid"]);
    }


    [Fact]
    public async Task ConcurrentWbiFailureIsSharedAndLaterExplicitRequestRecovers()
    {
        using var handler = new GatedHandler { Response = "{}" };
        using var client = new BiliHttpClient(handler);
        var auth = new BiliAuthenticator(null, null, null, client);
        var first = auth.InitializeWbiAsync();
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var others = Enumerable.Range(0, 8).Select(_ => auth.InitializeWbiAsync()).ToArray();
        handler.Release.TrySetResult();
        foreach (var task in others.Append(first))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        }

        Assert.Equal(1, handler.Count);
        handler.Response = Nav;
        await auth.InitializeWbiAsync();
        Assert.Equal(2, handler.Count);
        Assert.NotEmpty(Sign(auth));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WbiTransportFailureAllowsExplicitRecovery(bool httpFailure)
    {
        var fail = true;
        using var handler = new RecordingHttpMessageHandler(_ => !fail
            ? RecordingHttpMessageHandler.Json(Nav)
            : httpFailure ? RecordingHttpMessageHandler.Json(Nav, System.Net.HttpStatusCode.ServiceUnavailable)
            : throw new HttpRequestException("synthetic transport failure"));
        using var client = new BiliHttpClient(handler);
        var auth = new BiliAuthenticator(null, null, null, client);
        await Assert.ThrowsAsync<HttpRequestException>(() => auth.InitializeWbiAsync());
        Assert.Throws<InvalidOperationException>(() => Sign(auth));
        Assert.Single(handler.Requests);
        fail = false;
        await auth.InitializeWbiAsync();
        Assert.Equal(2, handler.Requests.Count);
        Assert.NotEmpty(Sign(auth));
        var independent = new BiliAuthenticator(null, null, null, client);
        Assert.Throws<InvalidOperationException>(() => Sign(independent));
    }

    private static string Sign(BiliAuthenticator auth) => auth.AuthorizeRequestParameters(
        new Dictionary<string, string> { ["wts"] = "1" },
        new Richasy.BiliKernel.Authenticator.BiliAuthorizeExecutionSettings
        {
            NeedRID = true,
            ApiType = Richasy.BiliKernel.Models.BiliApiType.None,
        })["w_rid"];

    internal sealed class GatedHandler : HttpMessageHandler
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Count;
        public string Response = Nav;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Count);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return RecordingHttpMessageHandler.Json(Response);
        }
    }
}
