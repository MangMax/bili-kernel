// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Services.Article.Core.Models;
using RichasyKernel;
using System.Text.Json;

namespace Richasy.BiliKernel.Services.Article.Core;

internal sealed class ArticleDiscoveryClient
{
    /// <summary>
    /// 正文接口返回可用数据的最大尝试次数（包含第一次）.
    /// </summary>
    private const int ContentFetchAttempts = 3;

    /// <summary>
    /// 正文重取线性退避的基础等待时间.
    /// </summary>
    private static readonly TimeSpan ContentFetchRetryDelay = TimeSpan.FromMilliseconds(200);

    private readonly BiliHttpClient _httpClient;
    private readonly BiliAuthenticator _authenticator;
    private readonly IBiliTokenResolver _tokenResolver;

    public ArticleDiscoveryClient(
        BiliHttpClient httpClient,
        BiliAuthenticator authenticator,
        IBiliTokenResolver tokenResolver)
    {
        _httpClient = httpClient;
        _authenticator = authenticator;
        _tokenResolver = tokenResolver;
    }

    public async Task<Dictionary<int, string>> GetHotCategoriesAsync(CancellationToken cancellationToken)
    {
        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(BiliApis.Article.HotCategories));
        _authenticator.AuthorizeRestRequest(request, settings: new BiliAuthorizeExecutionSettings { RequireToken = false });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseListArticleCategory).ConfigureAwait(false);
        return responseObj.Data is null || responseObj.Data.Count == 0
            ? throw new KernelException("热门专栏分区数据为空")
            : responseObj.Data.ToDictionary(p => Convert.ToInt32(p.Id), p => p.Name!);
    }

    public async Task<IReadOnlyList<Partition>> GetPartitionsAsync(CancellationToken cancellationToken)
    {
        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(BiliApis.Article.Categories));
        _authenticator.AuthorizeRestRequest(request, settings: new BiliAuthorizeExecutionSettings { RequireToken = false });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseListArticleCategory).ConfigureAwait(false);
        return responseObj.Data is null || responseObj.Data.Count == 0
            ? throw new KernelException("专栏分区数据为空")
            : (IReadOnlyList<Partition>)responseObj.Data.Select(p => p.ToPartition()).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<ArticleInformation>> GetHotArticlesAsync(int categoryId, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "cid", categoryId.ToString() },
        };

        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(BiliApis.Article.HotArticles));
        _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings { RequireToken = false });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseListArticle).ConfigureAwait(false);
        return responseObj.Data is null || responseObj.Data.Count == 0
            ? throw new KernelException("热门专栏文章数据为空")
            : responseObj.Data.Select(p => p.ToArticleInformation()).ToList().AsReadOnly();
    }

    public async Task<(IReadOnlyList<ArticleInformation>, IReadOnlyList<ArticleInformation>?)> GetPartitionArticlesAsync(Partition? partition, ArticleSortType? sort, int pageNumber, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "cid", partition?.Id ?? "0" },
            { "pn", pageNumber.ToString() },
            { "ps", "20" },
        };

        if (partition != null && sort != null)
        {
            parameters.Add("sort", ((int)sort).ToString());
        }

        var localToken = _tokenResolver.GetToken();
        if (localToken is not null)
        {
            parameters.Add("mid", localToken.UserId.ToString());
        }

        var uri = partition is null ? BiliApis.Article.Recommend : BiliApis.Article.ArticleList;
        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(uri));
        _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings { RequireToken = false });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (partition is null)
        {
            var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseArticleRecommendResponse).ConfigureAwait(false);
            var recommendArticles = responseObj.Data.Articles.Select(p => p.ToArticleInformation()).ToList().AsReadOnly();
            var topArticles = responseObj.Data.Ranks?.Select(p => p.ToArticleInformation()).ToList().AsReadOnly();
            return recommendArticles.Count == 0
                ? throw new KernelException("推荐专栏数据为空")
                : ((IReadOnlyList<ArticleInformation>, IReadOnlyList<ArticleInformation>?))(recommendArticles, topArticles);
        }
        else
        {
            var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseListArticle).ConfigureAwait(false);
            var articles = responseObj.Data.Select(p => p.ToArticleInformation()).ToList().AsReadOnly();
            return articles.Count == 0
                ? throw new KernelException("没有获取到有效的专栏文章列表，请稍后再试")
                : ((IReadOnlyList<ArticleInformation>, IReadOnlyList<ArticleInformation>?))(articles, null);
        }
    }

    public async Task<ArticleDetail?> GetArticleContentAsync(ArticleIdentifier article, CancellationToken cancellationToken)
    {
        var articleContent = await GetUsableArticleContentAsync(article, cancellationToken).ConfigureAwait(false);
        var articleView = await GetArticleMetadataAsync(article, cancellationToken).ConfigureAwait(false);
        return articleContent.ToArticleDetail(article, articleView);
    }

    /// <summary>
    /// 获取结构化正文；空数据或频控最多尝试三次.
    /// </summary>
    private async Task<ArticleContentResponse> GetUsableArticleContentAsync(ArticleIdentifier article, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parameters = new Dictionary<string, string>
            {
                { "id", article.Id },
                { "gaia_source", "main_web" },
            };
            using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(BiliApis.Article.ArticleContent));
            _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings { RequireToken = false });
            var json = await _httpClient.GetStringAsync(request, cancellationToken, ensureSuccess: true).ConfigureAwait(false);
            var response = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.BiliDataResponseReadinfo)
                ?? throw new KernelException("无法解析该文章的内容.");
            if (response.Code != 0)
            {
                if (response.Code is not (-509 or -799 or -412) || attempt >= ContentFetchAttempts)
                {
                    throw new KernelException($"获取文章正文失败：{response.Code} ({response.Message})");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(ContentFetchRetryDelay.TotalMilliseconds * attempt), cancellationToken).ConfigureAwait(false);
                continue;
            }

            // 保留适配器包装类型，待 #23 统一收口.
            var content = new ArticleContentResponse { readInfo = response.Data! };
            if (ArticleAdapter.HasUsableContent(content) || attempt >= ContentFetchAttempts)
            {
                return content;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(ContentFetchRetryDelay.TotalMilliseconds * attempt), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ArticleViewResponse> GetArticleMetadataAsync(ArticleIdentifier article, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "id", article.Id },
        };
        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri(BiliApis.Article.ArticleViewInfo));
        _authenticator.AuthorizeRestRequest(request, parameters);
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseArticleViewResponse).ConfigureAwait(false);
        return responseObj.Data ?? throw new KernelException("无法获取文章元数据");
    }
}
