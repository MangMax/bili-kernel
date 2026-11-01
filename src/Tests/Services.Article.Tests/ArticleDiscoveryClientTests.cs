// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Services.Article.Core;
using RichasyKernel;
using System.Net;
using System.Text;
using Xunit;

namespace Services.Article.Tests;

/// <summary>
/// Exercises the real article discovery client (HTTP client, authenticator, JSON
/// source generation and adapter) with a fake transport. The reading page
/// intermittently returns a structurally valid page whose readInfo is empty for
/// the same article; the client must re-fetch it instead of treating that state
/// as the final result.
/// </summary>
public sealed class ArticleDiscoveryClientTests
{
    private static readonly ArticleIdentifier Identifier = new("1001", "Synthetic article", null, null);

    [Fact]
    public async Task EmptyReadInfoIsRefetchedAndThenMapped()
    {
        var handler = new ArticleHandler([EmptyState, CompleteState]);

        using var client = CreateClient(handler, out var discovery);
        var detail = await discovery.GetArticleContentAsync(Identifier, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Author42", detail!.Author.Profile!.User.Name);
        Assert.Equal("<p>synthetic body</p>", detail.HtmlContent);
        Assert.Equal(2, handler.ContentRequests);
        Assert.Equal(1, handler.ViewRequests);
    }

    [Fact]
    public async Task UsableContentIsFetchedOnlyOnce()
    {
        var handler = new ArticleHandler([CompleteState]);

        using var client = CreateClient(handler, out var discovery);
        var detail = await discovery.GetArticleContentAsync(Identifier, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(1, handler.ContentRequests);
        Assert.Equal(1, handler.ViewRequests);
    }

    [Fact]
    public async Task PersistentlyEmptyReadInfoStopsAtTheBoundedAttempts()
    {
        var handler = new ArticleHandler([EmptyState, EmptyState, EmptyState, EmptyState]);

        using var client = CreateClient(handler, out var discovery);
        var error = await Assert.ThrowsAsync<KernelException>(
            () => discovery.GetArticleContentAsync(Identifier, CancellationToken.None));

        Assert.Contains("文章作者信息缺失", error.Message, StringComparison.Ordinal);
        Assert.Equal(3, handler.ContentRequests);
        // 元数据在正文重取用尽后仍保持原有的一次调用，错误仍由适配器给出.
        Assert.Equal(1, handler.ViewRequests);
    }

    private static BiliHttpClient CreateClient(ArticleHandler handler, out ArticleDiscoveryClient discovery)
    {
        var client = new BiliHttpClient(handler);
        discovery = new ArticleDiscoveryClient(client, new BiliAuthenticator(null, null), new EmptyTokenResolver());
        return client;
    }

    private const string EmptyState = """{"readInfo":{}}""";

    private const string CompleteState = """
        {
          "readInfo": {
            "id": 1001,
            "title": "Synthetic article",
            "publish_time": 1700000000,
            "author": {
              "mid": 42,
              "name": "Author42",
              "face": "https://example.invalid/face.png",
              "fans": 7,
              "level": 3
            },
            "content": "<p>synthetic body</p>",
            "tags": []
          }
        }
        """;

    private const string ViewState = """
        {
          "code": 0,
          "message": "0",
          "ttl": 1,
          "data": {
            "like": 1,
            "attention": false,
            "favorite": false,
            "coin": 0,
            "stats": {
              "view": 10,
              "favorite": 2,
              "like": 3,
              "reply": 4,
              "share": 5,
              "coin": 6
            },
            "mid": 42,
            "author_name": "Author42"
          }
        }
        """;

    private sealed class EmptyTokenResolver : IBiliTokenResolver
    {
        public BiliToken? GetToken() => null;

        public void SaveToken(BiliToken token)
        {
        }

        public void RemoveToken()
        {
        }
    }

    /// <summary>
    /// Fake transport: returns the queued reading-page states in order and a
    /// single, successful metadata response.
    /// </summary>
    private sealed class ArticleHandler(IEnumerable<string> contentStates) : HttpMessageHandler
    {
        private readonly Queue<string> _contentStates = new(contentStates);

        public int ContentRequests { get; private set; }

        public int ViewRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("read/native", StringComparison.Ordinal))
            {
                ContentRequests++;
                var state = _contentStates.Count > 0 ? _contentStates.Dequeue() : EmptyState;
                return Task.FromResult(Html($"<html><script>window.__INITIAL_STATE__ = {state};</script></html>"));
            }

            if (path.Contains("article/viewinfo", StringComparison.Ordinal))
            {
                ViewRequests++;
                return Task.FromResult(Json(ViewState));
            }

            throw new InvalidOperationException($"Unexpected request path '{path}'.");
        }

        private static HttpResponseMessage Html(string html)
            => new(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };

        private static HttpResponseMessage Json(string json)
            => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
