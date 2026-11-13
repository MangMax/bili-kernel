// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Bilibili.App.Interfaces.V1;
using Bilibili.Polymer.App.Search.V1;
using Richasy.BiliKernel.Adapters;
using Richasy.BiliKernel.Models.Search;

namespace Richasy.BiliKernel.Services.Search.Core;

internal static class SearchAdapter
{
    // 现行 Web 端点不返回 position / hot_id，榜单顺序即名次，用 keyword 兜底标识.
    public static HotSearchItem ToHotSearchItem(this WebHotSearchItem item, int order)
    {
        var keyword = item.Keyword ?? string.Empty;
        return new HotSearchItem(
            item.HotId?.ToString() ?? keyword,
            item.Position > 0 ? item.Position : order,
            keyword,
            item.ShowName ?? keyword,
            item.ShowLiveIcon,
            item.ResourceId?.ToString(),
            string.IsNullOrEmpty(item.Icon) ? null : item.Icon.ToImage());
    }

    public static SearchRecommendItem ToSearchRecommendItem(this WebSearchRecommendItem item)
    {
        return new SearchRecommendItem(
            item.Id.ToString(),
            item.Position,
            item.Keyword,
            item.Title
        );
    }

    public static SearchPartition ToSearchPartition(this Nav nav)
    {
        return new SearchPartition(
            nav.Type,
            nav.Name,
            nav.Pages is 0 ? default : nav.Pages,
            nav.Total is 0 ? default : nav.Total);
    }

    public static SearchResultItem ToSearchResultItem(this Item item)
    {
        var season = item.ToSeasonInformation();
        var article = item.ToArticleInformation();
        var live = item.ToLiveInformation();
        var user = item.ToUserCard();
        return new SearchResultItem(default, season, live, article, user);
    }

    public static SearchSuggestItem ToSearchSuggest(this ResultItem item)
        => new SearchSuggestItem(item.Title, item.Keyword);
}
