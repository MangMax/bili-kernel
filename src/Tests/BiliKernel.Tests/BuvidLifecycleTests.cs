using Richasy.BiliKernel.Http;
using Xunit;

namespace Richasy.BiliKernel.Tests;

public sealed class BuvidLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdatedCookieIdentityIsActivatedWithoutRestoringPreviousIdentity(bool previousFailure)
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var response = previousFailure ? "{\"code\":-1}" : "{\"code\":0}";
        using var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(response));
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["buvid3"] = "ACCOUNT-A3", ["buvid4"] = "ACCOUNT-A4", ["SESSDATA"] = "session-a" });
        var store = new InMemoryBuvidStore("PERSISTED3", "PERSISTED4");
        using var manager = new BuvidManager(cookies, store, handler, () => now);
        await manager.EnsureAsync();
        Assert.Single(handler.Requests);

        cookies.SaveCookies(new Dictionary<string, string> { ["buvid3"] = "ACCOUNT-B3", ["SESSDATA"] = "session-b" });
        if (previousFailure) now = now.AddMinutes(1);
        response = "{\"code\":0}";
        await manager.EnsureAsync();
        await manager.EnsureAsync();

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("ACCOUNT-B3", manager.Buvid3);
        Assert.Null(manager.Buvid4);
        Assert.Equal("ACCOUNT-B3", cookies.Cookies["buvid3"]);
        Assert.Equal("session-b", cookies.Cookies["SESSDATA"]);
        Assert.False(cookies.Cookies.ContainsKey("buvid4"));
        Assert.Contains("buvid3=ACCOUNT-B3", handler.Requests[1].Cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCOUNT-A", handler.Requests[1].Cookie, StringComparison.Ordinal);
        Assert.Equal("PERSISTED3", store.Buvid3);
    }

    [Theory]
    [InlineData("{\"code\":-1}", 200)]
    [InlineData("{\"code\":0}", 500)]
    [InlineData("{}", 200)]
    [InlineData("invalid", 200)]
    public async Task FailedActivationRetriesOnlyAfterExplicitCallAtCooldownBoundary(string failure, int status)
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var response = failure;
        var responseStatus = (System.Net.HttpStatusCode)status;
        using var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(response, responseStatus));
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "synthetic", ["bili_jct"] = "csrf" });
        var store = new InMemoryBuvidStore("SAVED3", "SAVED4");
        using var manager = new BuvidManager(cookies, store, handler, () => now);
        await manager.EnsureAsync();
        Assert.Single(handler.Requests);
        now = now.AddMinutes(1).AddTicks(-1);
        await manager.EnsureAsync();
        Assert.Single(handler.Requests);
        now = now.AddTicks(1);
        response = "{\"code\":0}";
        responseStatus = System.Net.HttpStatusCode.OK;
        await manager.EnsureAsync();
        Assert.Equal(2, handler.Requests.Count);
        await manager.EnsureAsync();
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => {
            Assert.Equal("POST", r.Method);
            Assert.Contains("buvid3=SAVED3", r.Cookie, StringComparison.Ordinal);
            Assert.Contains("buvid4=SAVED4", r.Cookie, StringComparison.Ordinal);
        });
        Assert.Equal("SAVED3", store.Buvid3);
        Assert.Equal("SAVED4", store.Buvid4);
        Assert.Equal("synthetic", cookies.Cookies["SESSDATA"]);
        Assert.Equal("csrf", cookies.Cookies["bili_jct"]);
    }

    [Fact]
    public async Task ConcurrentPersistedActivationRunsOnce()
    {
        using var handler = new WbiLifecycleTests.GatedHandler { Response = "{\"code\":0}" };
        using var manager = new BuvidManager(null, new InMemoryBuvidStore("SAVED3", "SAVED4"), handler);
        var first = manager.EnsureAsync();
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var others = Enumerable.Range(0, 8).Select(_ => manager.EnsureAsync()).ToArray();
        handler.Release.TrySetResult();
        await Task.WhenAll(others.Append(first));
        await manager.EnsureAsync();
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task CancelledActivationCanRecoverWithoutCooldownOrIdentityChange()
    {
        using var handler = new WbiLifecycleTests.GatedHandler { Response = "{\"code\":0}" };
        var store = new InMemoryBuvidStore("SAVED3", "SAVED4");
        using var manager = new BuvidManager(null, store, handler);
        using var cancellation = new CancellationTokenSource();
        var first = manager.EnsureAsync(cancellation.Token);
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        handler.Release.TrySetResult();
        await manager.EnsureAsync();
        await manager.EnsureAsync();
        Assert.Equal(2, handler.Count);
        Assert.Equal("SAVED3", manager.Buvid3);
        Assert.Equal("SAVED4", manager.Buvid4);
    }

    [Fact]
    public async Task CancelledActivationWaiterDoesNotCancelOwner()
    {
        using var handler = new WbiLifecycleTests.GatedHandler { Response = "{\"code\":0}" };
        using var manager = new BuvidManager(null, new InMemoryBuvidStore("SAVED3"), handler);
        var owner = manager.EnsureAsync();
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource();
        var waiter = manager.EnsureAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        Assert.False(owner.IsCompleted);
        handler.Release.TrySetResult();
        await owner.ConfigureAwait(true);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task ExistingCookieIdentityIsNotReplacedByDifferentPersistedIdentity()
    {
        using var handler = new RecordingHttpMessageHandler();
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["buvid3"] = "COOKIE3", ["buvid4"] = "COOKIE4", ["SESSDATA"] = "synthetic" });
        var store = new InMemoryBuvidStore("STORED3", "STORED4");
        using var manager = new BuvidManager(cookies, store, handler);
        await manager.EnsureAsync();
        Assert.Equal("COOKIE3", manager.Buvid3);
        Assert.Equal("COOKIE4", manager.Buvid4);
        Assert.Equal("COOKIE3", cookies.Cookies["buvid3"]);
        Assert.Equal("synthetic", cookies.Cookies["SESSDATA"]);
        Assert.Equal("STORED3", store.Buvid3);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Contains("buvid3=COOKIE3", request.Cookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CookieIdentityWithoutBuvid4DoesNotBorrowDifferentStoredBuvid4()
    {
        using var handler = new RecordingHttpMessageHandler();
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string> { ["buvid3"] = "COOKIE3" });
        using var manager = new BuvidManager(cookies, new InMemoryBuvidStore("OTHER3", "OTHER4"), handler);
        await manager.EnsureAsync();
        Assert.Null(manager.Buvid4);
        Assert.DoesNotContain("buvid4=", Assert.Single(handler.Requests).Cookie, StringComparison.Ordinal);
        Assert.False(cookies.Cookies.ContainsKey("buvid4"));
    }
}
