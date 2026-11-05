// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Xunit;
using System.Net;
using System.Text;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Services.User;
using RichasyKernel;

namespace Richasy.BiliKernel.Tests;

public sealed class ViewLaterReadTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public async Task ListReadUsesCookieWithoutAppTokenAndTranslatesPageIndex(int page, int wirePage)
    {
        using var handler = new Handler("""
            {"code":0,"data":{"count":1,"list":[{"aid":123,"title":"Video","pic":"https://example.com/cover.jpg",
            "duration":60,"pubdate":1700000000,"owner":{"mid":42,"name":"作者"},"stat":{},"tname":"科技"}]}}
            """);
        using var client = new BiliHttpClient(handler);
        var auth = new BiliAuthenticator(new Cookies(), new Token(), httpClient: client);
        var service = new ViewLaterService(client, new NoTokenRefresh(), auth);
        var (videos, count, next) = await service.GetViewLaterSetAsync(page);
        Assert.Equal("123", Assert.Single(videos!).Identifier.Id);
        Assert.Equal(1, count);
        Assert.Equal(page + 1, next);
        Assert.Contains("pn=" + wirePage, handler.ListQuery);
        Assert.Contains("w_rid=", handler.ListQuery);
        Assert.DoesNotContain("access_key", handler.ListQuery);
        Assert.Contains("SESSDATA=web-test-session", handler.Cookie);
    }

    [Theory]
    [InlineData("{\"code\":-663,\"message\":\"auth failed\"}")]
    [InlineData("{\"code\":0,\"data\":null}")]
    public async Task RejectedOrMissingListDoesNotBecomeSuccessfulEmptyResult(string json)
    {
        using var handler = new Handler(json);
        using var client = new BiliHttpClient(handler);
        var service = new ViewLaterService(client, new NoTokenRefresh(), new BiliAuthenticator(new Cookies(), new Token(), httpClient: client));
        await Assert.ThrowsAsync<KernelException>(() => service.GetViewLaterSetAsync());
    }

    [Fact]
    public async Task SuccessfulEmptyListRemainsEmpty()
    {
        using var handler = new Handler("{\"code\":0,\"data\":{\"count\":0,\"list\":[]}}");
        using var client = new BiliHttpClient(handler);
        var service = new ViewLaterService(client, new NoTokenRefresh(), new BiliAuthenticator(new Cookies(), new Token(), httpClient: client));
        var (videos, count, _) = await service.GetViewLaterSetAsync();
        Assert.Empty(videos!);
        Assert.Equal(0, count);
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
        public string ListQuery { get; private set; } = "";
        public string Cookie { get; private set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = """{"code":0,"data":{"wbi_img":{"img_url":"https://example.com/7cd084941338484aae1ad9425b84077c.png","sub_url":"https://example.com/4932caff0ff746eab6f01bf08b70ac45.png"}}}""";
            if (request.RequestUri!.AbsolutePath == "/x/v2/history/toview/web")
            {
                ListQuery = request.RequestUri.Query;
                Cookie = string.Join(";", request.Headers.GetValues("Cookie"));
                json = response;
            }
            else Assert.Equal("/x/web-interface/nav", request.RequestUri.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
