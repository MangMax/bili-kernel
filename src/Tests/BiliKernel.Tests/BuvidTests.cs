// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Services.User;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 设备标识生命周期测试。
/// </summary>
public sealed class BuvidTests
{
    private const string SpiResponse = @"{""code"":0,""data"":{""b_3"":""BUV3"",""b_4"":""BUV4""}}";

    [Fact]
    public async Task WebRequestCarriesBuvidCookie()
    {
        var (handler, service) = CreateService();

        await service.AddAsync("42");

        var business = handler.Requests.Last();
        Assert.Equal("/x/v2/history/toview/add", business.Path);
        Assert.Contains("buvid3=BUV3", business.Cookie, StringComparison.Ordinal);
        Assert.Contains("buvid4=BUV4", business.Cookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuvidIsFetchedOnceAndActivatedOnce()
    {
        var (handler, service) = CreateService();

        await service.AddAsync("1");
        await service.AddAsync("2");

        Assert.Equal(1, handler.Requests.Count(p => p.Path.Contains("/x/frontend/finger/spi", StringComparison.Ordinal)));
        Assert.Equal(1, handler.Requests.Count(p => p.Path.Contains("/x/internal/gaia-gateway/", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task PersistedBuvidIsReusedWithoutRegistration()
    {
        var (handler, service) = CreateService(new InMemoryBuvidStore("STORED3", "STORED4"));

        await service.AddAsync("42");

        Assert.DoesNotContain(handler.Requests, p => p.Path.Contains("/x/frontend/finger/spi", StringComparison.Ordinal));
        Assert.Contains("buvid3=STORED3", handler.Requests.Last().Cookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuvidFromCookiesWinsOverRegistration()
    {
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string, string>(StringComparer.Ordinal) { { "buvid3", "COOKIE3" } });
        var (handler, service) = CreateService(cookies: cookies);

        await service.AddAsync("42");

        Assert.DoesNotContain(handler.Requests, p => p.Path.Contains("/x/frontend/finger/spi", StringComparison.Ordinal));
        Assert.Contains("buvid3=COOKIE3", handler.Requests.Last().Cookie, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientWithoutHandlerDoesNotThrow()
    {
        using var client = new BiliHttpClient();
        Assert.NotNull(client);
    }

    private static (RecordingHttpMessageHandler Handler, ViewLaterService Service) CreateService(
        InMemoryBuvidStore? store = null,
        FakeCookiesResolver? cookies = null)
    {
        var handler = new RecordingHttpMessageHandler()
            .With("/x/frontend/finger/spi", SpiResponse)
            .With("/x/internal/gaia-gateway/", @"{""code"":0}");
        cookies ??= new FakeCookiesResolver();
        var buvid = new BuvidManager(cookies, store ?? new InMemoryBuvidStore(), handler);
        var http = new BiliHttpClient(handler, buvid);
        var authenticator = new BiliAuthenticator(cookies, new FakeTokenResolver(), buvid);
        return (handler, new ViewLaterService(http, new FakeAuthenticationService(), authenticator));
    }
}