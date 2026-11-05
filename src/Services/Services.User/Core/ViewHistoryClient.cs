// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Bilibili.App.Interfaces.V1;
using Richasy.BiliKernel.Adapters;
using System.Diagnostics;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Services.User.Core.Models;
using RichasyKernel;

namespace Richasy.BiliKernel.Services.User.Core;

internal sealed class ViewHistoryClient
{
    private readonly BiliHttpClient _httpClient;
    private readonly IAuthenticationService _authenticationService;
    private readonly BiliAuthenticator _authenticator;

    public ViewHistoryClient(
        BiliHttpClient httpClient,
        IAuthenticationService authenticationService,
        BiliAuthenticator authenticator)
    {
        _httpClient = httpClient;
        _authenticationService = authenticationService;
        _authenticator = authenticator;
    }

    public async Task<ViewHistoryGroup> GetHistorySetAsync(ViewHistoryTabType tabType, long offset, CancellationToken cancellationToken)
    {
        if (tabType == ViewHistoryTabType.Video)
        {
            try
            {
                return await GetWebHistorySetAsync(tabType, offset, cancellationToken).ConfigureAwait(false);
            }
            catch (KernelException ex)
            {
                // Web 端点异常时保留 gRPC 回退.
                Debug.WriteLine($"Web 观看历史不可用，回退 gRPC：{ex.Message}");
            }
        }

        var business = GetBusiness(tabType);
        var req = new CursorReq
        {
            Business = business,
            Cursor = new Cursor { Max = offset }
        };

        var request = BiliHttpClient.CreateRequest(new Uri(BiliApis.Account.HistoryCursor), req);
        _authenticator.AuthorizeGrpcRequest(request);
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var data = await BiliHttpClient.ParseAsync(response, CursorV2Reply.Parser).ConfigureAwait(false);

        // 空结果.
        if (data.Cursor is null && data.Items.Count == 0)
        {
            return new ViewHistoryGroup(tabType, Array.Empty<VideoInformation>(), Array.Empty<LiveInformation>(), Array.Empty<ArticleInformation>(), 0);
        }

        var nextOffset = data.Cursor.Max;
        var videos = data.Items.Where(p => p.CardItemCase == CursorItem.CardItemOneofCase.CardUgc)
            .Select(p => p.ToVideoInformation()).ToList().AsReadOnly();
        var lives = data.Items.Where(p => p.CardItemCase == CursorItem.CardItemOneofCase.CardLive)
            .Select(p => p.ToLiveInformation()).ToList().AsReadOnly();
        var articles = data.Items.Where(p => p.CardItemCase == CursorItem.CardItemOneofCase.CardArticle)
            .Select(p => p.ToArticleInformation()).ToList().AsReadOnly();
        return new ViewHistoryGroup(tabType, videos, lives, articles, nextOffset);
    }

    public async Task RemoveHistoryItemAsync(string itemId, ViewHistoryTabType tab, CancellationToken cancellationToken)
    {
        var business = GetBusiness(tab);
        var req = new DeleteReq();
        req.HisInfo.Add(new HisInfo
        {
            Business = business,
            Kid = Convert.ToInt64(itemId),
        });

        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "kid", $"{business}_{itemId}" },
            };

            var webRequest = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Post, new Uri(BiliApis.Account.WebHistoryDelete));
            _authenticator.AuthorizeRestRequest(webRequest, parameters, BiliAuthorizeExecutionSettings.CookieOnly(needCSRF: true));
            await _httpClient.SendAsync(webRequest, cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (KernelException ex)
        {
            // Web 端点失败时回退 gRPC.
            Debug.WriteLine($"Web 删除历史失败，回退 gRPC：{ex.Message}");
        }

        var request = BiliHttpClient.CreateRequest(new Uri(BiliApis.Account.DeleteHistoryItem), req);
        _authenticator.AuthorizeGrpcRequest(request);
        await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task CleanHistoryAsync(ViewHistoryTabType tab, CancellationToken cancellationToken)
    {
        var business = GetBusiness(tab);
        var req = new ClearReq
        {
            Business = business,
        };

        // Web clear has no category boundary. Preserve category-scoped gRPC semantics.
        if (tab == ViewHistoryTabType.All)
        {
            try
            {
                var webRequest = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Post, new Uri(BiliApis.Account.WebHistoryClear));
                _authenticator.AuthorizeRestRequest(webRequest, new Dictionary<string, string>(), BiliAuthorizeExecutionSettings.CookieOnly(needCSRF: true));
                await _httpClient.SendAsync(webRequest, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (KernelException ex)
            {
                // Web 端点失败时回退 gRPC.
                Debug.WriteLine($"Web 清空历史失败，回退 gRPC：{ex.Message}");
            }
        }

        var request = BiliHttpClient.CreateRequest(new Uri(BiliApis.Account.ClearHistory), req);
        _authenticator.AuthorizeGrpcRequest(request);
        await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> GetIsHistoryEnabledAsync(CancellationToken cancellationToken)
    {
        var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Get, new Uri(BiliApis.Account.HistoryRecordOption));
        _authenticator.AuthorizeRestRequest(request, default, new BiliAuthorizeExecutionSettings { ApiType = BiliApiType.Web, RequireCookie = true, ForceNoToken = true });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseBoolean).ConfigureAwait(false);
        return !responseObj.Data;
    }

    public async Task SetHistoryRecordOptionAsync(bool stopRecord, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "switch", stopRecord.ToString().ToLowerInvariant() },
        };

        var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Post, new Uri(BiliApis.Account.SetHistoryRecordOption));
        _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings { ApiType = BiliApiType.Web, NeedCSRF = true, RequireCookie = true, ForceNoToken = true });
        await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ViewHistoryGroup> GetWebHistorySetAsync(ViewHistoryTabType tabType, long offset, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "max", offset.ToString() },
            { "view_at", "0" },
            { "ps", "30" },
            { "business", string.Empty },
        };

        var request = BiliHttpClient.CreateRequest(System.Net.Http.HttpMethod.Get, new Uri(BiliApis.Account.WebHistoryCursor));
        _authenticator.AuthorizeRestRequest(request, parameters, new BiliAuthorizeExecutionSettings { ApiType = BiliApiType.Web, RequireCookie = true, ForceNoToken = true });
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseWebHistoryResponse).ConfigureAwait(false);
        var list = responseObj?.Data?.List ?? [];
        if (list.Count == 0)
        {
            return new ViewHistoryGroup(tabType, Array.Empty<VideoInformation>(), Array.Empty<LiveInformation>(), Array.Empty<ArticleInformation>(), 0);
        }

        var videos = list
            .Where(p => p.History != null && (string.IsNullOrEmpty(p.History.Business) || p.History.Business == "archive"))
            .Select(p => ToVideoInformation(p))
            .ToList()
            .AsReadOnly();
        var nextOffset = responseObj?.Data?.Cursor?.Max ?? 0;
        return new ViewHistoryGroup(tabType, videos, Array.Empty<LiveInformation>(), Array.Empty<ArticleInformation>(), nextOffset);
    }

    private static VideoInformation ToVideoInformation(WebHistoryItem item)
    {
        var cover = string.IsNullOrEmpty(item.Cover) ? null : item.Cover.ToImage();
        var identifier = new MediaIdentifier(item.History?.Oid.ToString() ?? string.Empty, item.Title, cover);
        var publisher = new PublisherProfile(new UserProfile("0"));
        return new VideoInformation(identifier, publisher, item.Duration, item.BvId);
    }

    private static string GetBusiness(ViewHistoryTabType tabType)
    {
        return tabType switch
        {
            ViewHistoryTabType.Video => "archive",
            ViewHistoryTabType.Article => "article",
            ViewHistoryTabType.Live => "live",
            _ => "all",
        };
    }
}
