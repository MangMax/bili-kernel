// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Services.Article;
using RichasyKernel;
using Xunit;

namespace Services.Article.Tests;

public sealed class ArticlePaginationResponseTests
{
    private const string ArticleJson = """{"id":1001,"title":"Synthetic article","author":{"mid":42,"name":"Author42"},"category":{"id":2,"name":"动画"},"stats":{"view":7},"publish_time":1700000000}""";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonEmptyPagesStillMapArticlesAndAdvanceCursor(bool recommend)
    {
        var data = recommend ? $$"""{"articles":[{{ArticleJson}}],"ranks":[]}""" : $"[{ArticleJson}]";
        using var handler = new Handler($$"""{"code":0,"data":{{data}}}""");
        using var http = new BiliHttpClient(handler);
        var tokens = new EmptyTokenResolver();
        var discovery = new ArticleDiscoveryService(http, new BiliAuthenticator(null, tokens), tokens);

        var (articles, nextPage) = recommend
            ? await RecommendPage(discovery)
            : await discovery.GetPartitionArticlesAsync(new Partition("2", "动画"), ArticleSortType.Default);

        var article = Assert.Single(articles);
        Assert.Equal("1001", article.Identifier.Id);
        Assert.Equal("Synthetic article", article.Identifier.Title);
        Assert.Equal(1, nextPage);
        Assert.Equal(1, handler.Requests);

        static async Task<(IReadOnlyList<Richasy.BiliKernel.Models.Article.ArticleInformation>, int)> RecommendPage(ArticleDiscoveryService service)
        {
            var (items, _, page) = await service.GetRecommendArticlesAsync();
            return (items, page);
        }
    }

    [Fact]
    public async Task EmptyRecommendationPagePreservesAvailableRanks()
    {
        using var handler = new Handler("{\"code\":0,\"data\":{\"articles\":null,\"ranks\":[" + ArticleJson + "]}}");
        using var http = new BiliHttpClient(handler);
        var tokens = new EmptyTokenResolver();
        var discovery = new ArticleDiscoveryService(http, new BiliAuthenticator(null, tokens), tokens);

        var (articles, ranks, _) = await discovery.GetRecommendArticlesAsync();

        Assert.Empty(articles);
        Assert.Equal("1001", Assert.Single(ranks!).Identifier.Id);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    public async Task SuccessfulEmptyPartitionPageReturnsEndOfList(string data)
    {
        using var handler = new Handler($$"""{"aids_len":60,"code":0,"data":{{data}},"message":"OK"}""");
        using var http = new BiliHttpClient(handler);
        var tokens = new EmptyTokenResolver();
        var discovery = new ArticleDiscoveryService(http, new BiliAuthenticator(null, tokens), tokens);

        var (articles, nextPage) = await discovery.GetPartitionArticlesAsync(new Partition("2", "动画"), ArticleSortType.Default, 4);

        Assert.Empty(articles);
        Assert.Equal(5, nextPage);
        Assert.Equal(1, handler.Requests);
        Assert.Contains("pn=5", handler.Query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"articles\":null,\"ranks\":null}")]
    [InlineData("{\"articles\":[],\"ranks\":[]}")]
    public async Task SuccessfulEmptyRecommendationPageReturnsEndOfList(string data)
    {
        using var handler = new Handler($$"""{"code":0,"data":{{data}},"message":"OK"}""");
        using var http = new BiliHttpClient(handler);
        var tokens = new EmptyTokenResolver();
        var discovery = new ArticleDiscoveryService(http, new BiliAuthenticator(null, tokens), tokens);

        var (articles, _, nextPage) = await discovery.GetRecommendArticlesAsync(4);

        Assert.Empty(articles);
        Assert.Equal(5, nextPage);
        Assert.Equal(1, handler.Requests);
    }

    [Theory]
    [InlineData(false, -101)]
    [InlineData(true, -101)]
    [InlineData(false, -412)]
    [InlineData(true, -412)]
    public async Task BusinessFailureIsNotTreatedAsEndOfList(bool recommend, int code)
    {
        using var handler = new Handler($$"""{"code":{{code}},"data":null,"message":"synthetic denied"}""");
        using var http = new BiliHttpClient(handler);
        var tokens = new EmptyTokenResolver();
        var discovery = new ArticleDiscoveryService(http, new BiliAuthenticator(null, tokens), tokens);

        var error = await Assert.ThrowsAsync<KernelException>(async () =>
        {
            if (recommend) { await discovery.GetRecommendArticlesAsync(); }
            else { await discovery.GetPartitionArticlesAsync(new Partition("2", "动画"), ArticleSortType.Default); }
        });

        Assert.Contains(code.ToString(), Assert.IsType<Exception>(error.InnerException).Message, StringComparison.Ordinal);
        Assert.Contains("synthetic denied", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, handler.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpFailureIsNotTreatedAsEndOfList(bool recommend)
    {
        using var handler = new Handler("""{"code":0,"data":null}""") { Status = HttpStatusCode.Forbidden };
        using var http = new BiliHttpClient(handler);
        var tokens = new EmptyTokenResolver();
        var discovery = new ArticleDiscoveryService(http, new BiliAuthenticator(null, tokens), tokens);

        var error = await Assert.ThrowsAsync<KernelException>(async () =>
        {
            if (recommend) { await discovery.GetRecommendArticlesAsync(); }
            else { await discovery.GetPartitionArticlesAsync(new Partition("2", "动画"), ArticleSortType.Default); }
        });

        Assert.Equal(HttpStatusCode.Forbidden, Assert.IsType<HttpRequestException>(error.InnerException).StatusCode);
        Assert.Equal(1, handler.Requests);
    }

    private sealed class Handler(string body) : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public int Requests { get; private set; }
        public string Query { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Assert.Contains(request.RequestUri!.AbsolutePath, new[] { "/x/article/recommends", "/x/article/recommends/plus" });
            Query = request.RequestUri.Query;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class EmptyTokenResolver : IBiliTokenResolver
    {
        public BiliToken? GetToken() => null;
        public void SaveToken(BiliToken token) { }
        public void RemoveToken() { }
    }
}
