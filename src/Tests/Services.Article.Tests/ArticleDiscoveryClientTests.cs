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

    [Theory]
    [InlineData("{\"author\": {\"mid\": 42}, \"opus\": {\"content\": {\"paragraphs\": [{\"para_type\": 99}]}}}")]
    [InlineData("{\"author\": {\"mid\": 42}, \"content\": \"<p></p>\"}")]
    [InlineData("{\"author\": {\"mid\": 42}, \"content\": \"<p>fallback</p>\", \"opus\": {}}")]
    public async Task UnrenderableBodyIsRefetched(string readInfo)
    {
        // 与 ToArticleDetail 的 opus 优先 + FormatHtmlContent 规则一致：
        // 未知段落独占、格式化后为空的正文、opus 存在但为空时都应重取。
        var handler = new ArticleHandler(["{\"readInfo\":" + readInfo + "}", CompleteState]);
        using var client = CreateClient(handler, out var discovery);
        var detail = await discovery.GetArticleContentAsync(Identifier, CancellationToken.None);
        Assert.Equal("<p>synthetic body</p>", detail!.HtmlContent);
        Assert.Equal(2, handler.ContentRequests);
        Assert.Equal(1, handler.ViewRequests);
    }

    [Theory]
    [InlineData("{\"author\": {\"mid\": 42}, \"opus\": {\"content\": {\"paragraphs\": [{\"para_type\": 2, \"pic\": {\"pics\": [{\"url\": \"https://example.invalid/image.png\", \"width\": 640, \"height\": 480}]}}]}}}", "https://example.invalid/image.png")]
    [InlineData("{\"author\": {\"mid\": 42}, \"opus\": {\"content\": {\"paragraphs\": [{\"para_type\": 3, \"line\": {\"pic\": {\"url\": \"https://example.invalid/line.png\", \"height\": 20}}}]}}}", "https://example.invalid/line.png")]
    [InlineData("{\"author\": {\"mid\": 42}, \"opus\": {\"content\": {\"paragraphs\": [{\"para_type\": 99}, {\"para_type\": 1, \"text\": {\"nodes\": [{\"node_type\": 1, \"word\": {\"words\": \"mixed body\"}}]}}]}}}", "mixed body")]
    public async Task RenderableMediaAndMixedContentAreFetchedOnlyOnce(string readInfo, string expected)
    {
        // 合法图片、分割线与有效混合文本不能被可用性判断误拒而多余重取。
        var handler = new ArticleHandler(["{\"readInfo\":" + readInfo + "}", CompleteState]);
        using var client = CreateClient(handler, out var discovery);
        var detail = await discovery.GetArticleContentAsync(Identifier, CancellationToken.None);
        Assert.Contains(expected, detail!.HtmlContent, StringComparison.Ordinal);
        Assert.Equal(1, handler.ContentRequests);
        Assert.Equal(1, handler.ViewRequests);
    }

    [Fact]
    public async Task CancellationDuringDelayPreventsMoreRequests()
    {
        // 调用方取消后，重取间隔与元数据请求都不应继续。
        var handler = new ArticleHandler([EmptyState, CompleteState]);
        using var client = CreateClient(handler, out var discovery);
        using var cts = new CancellationTokenSource();
        var task = discovery.GetArticleContentAsync(Identifier, cts.Token);
        Assert.True(SpinWait.SpinUntil(() => handler.ContentRequests == 1, TimeSpan.FromSeconds(5)));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, handler.ContentRequests);
        Assert.Equal(0, handler.ViewRequests);
    }

    [Fact]
    public async Task HttpForbiddenWithEmptyStateMustNotRetry()
    {
        // 403/认证等永久 HTTP 错误即使带空状态也只失败一次，不进入重取。
        var handler = new ArticleHandler([EmptyState]) { Status = HttpStatusCode.Forbidden };
        using var client = CreateClient(handler, out var discovery);
        await Assert.ThrowsAnyAsync<Exception>(() => discovery.GetArticleContentAsync(Identifier, CancellationToken.None));
        Assert.Equal(1, handler.ContentRequests);
        Assert.Equal(0, handler.ViewRequests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-412)]
    [InlineData(-509)]
    [InlineData(-799)]
    public async Task MetadataFailurePreservesBodyAndLeavesStatisticsAndStateUnknown(int code)
    {
        var state = CompleteState.Replace("synthetic body</p>",
            """synthetic body</p><img src=\"https://example.invalid/image.png\"/>""");
        var handler = new ArticleHandler([state]) { Metadata = $$"""{"code":{{code}},"message":"limited","data":null}""" };
        using var client = CreateClient(handler, out var discovery);
        var detail = await discovery.GetArticleContentAsync(Identifier, CancellationToken.None);

        var successHandler = new ArticleHandler([state]);
        using var successClient = CreateClient(successHandler, out var successDiscovery);
        var success = await successDiscovery.GetArticleContentAsync(Identifier, CancellationToken.None);
        Assert.Equal(success!.HtmlContent, detail!.HtmlContent);
        Assert.Contains("https://example.invalid/image.png", detail.HtmlContent, StringComparison.Ordinal);
        Assert.Equal("Author42", detail.Author.Profile!.User.Name);
        Assert.Equal(success.PublishTime, detail.PublishTime);
        Assert.Null(detail.CommunityInformation.ViewCount);
        Assert.Null(detail.CommunityInformation.LikeCount);
        Assert.Null(detail.CommunityInformation.FavoriteCount);
        Assert.Null(detail.CommunityInformation.CommentCount);
        Assert.Null(detail.CommunityInformation.ShareCount);
        Assert.Null(detail.CommunityInformation.CoinCount);
        Assert.Null(detail.IsLiked);
        Assert.Null(detail.IsFavorited);
        Assert.True(success.IsLiked);
        Assert.False(success.IsFavorited);
        Assert.Equal(10, success.CommunityInformation.ViewCount);
        Assert.Equal(1, handler.ContentRequests);
        Assert.Equal(1, handler.ViewRequests);
    }

    [Fact]
    public async Task CancellationDuringMetadataRequestIsNotDegraded()
    {
        using var cts = new CancellationTokenSource();
        var handler = new ArticleHandler([CompleteState]) { OnMetadataRequest = cts.Cancel };
        using var client = CreateClient(handler, out var discovery);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => discovery.GetArticleContentAsync(Identifier, cts.Token));
        Assert.Equal(1, handler.ContentRequests);
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

        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        public string Metadata { get; init; } = ViewState;

        public Action? OnMetadataRequest { get; init; }

        public int ContentRequests { get; private set; }

        public int ViewRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("read/native", StringComparison.Ordinal))
            {
                ContentRequests++;
                var state = _contentStates.Count > 0 ? _contentStates.Dequeue() : EmptyState;
                var response = Html($"<html><script>window.__INITIAL_STATE__ = {state};</script></html>");
                response.StatusCode = Status;
                return Task.FromResult(response);
            }

            if (path.Contains("article/viewinfo", StringComparison.Ordinal))
            {
                ViewRequests++;
                OnMetadataRequest?.Invoke();
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Json(Metadata));
            }

            throw new InvalidOperationException($"Unexpected request path '{path}'.");
        }

        private static HttpResponseMessage Html(string html)
            => new(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };

        private static HttpResponseMessage Json(string json)
            => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
