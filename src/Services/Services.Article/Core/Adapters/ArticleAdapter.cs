// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Adapters;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Base;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Services.Article.Core.Models;
using RichasyKernel;
using System.Net;

namespace Richasy.BiliKernel.Services.Article.Core;

internal static class ArticleAdapter
{
    public static Partition ToPartition(this ArticleCategory category)
    {
        var id = category.Id.ToString();
        var name = category.Name;
        var children = category.Children?.Select(p => p.ToPartition()).ToList();
        var parentId = category.ParentId?.ToString();
        return new Partition(id, name, default, children, parentId);
    }

    public static ArticleInformation ToArticleInformation(this Article article)
    {
        var identifier = new ArticleIdentifier(article.Id.ToString(), article.Title, article.Summary, article.CoverUrls?.FirstOrDefault()?.ToArticleCover());
        var user = UserAdapterBase.CreateUserProfile(article.Publisher.Mid, article.Publisher.Publisher, article.Publisher.PublisherAvatar, 48d);
        var publishTime = DateTimeOffset.FromUnixTimeSeconds(article.PublishTime).ToLocalTime();
        var communityInfo = new ArticleCommunityInformation(article.Id.ToString(), article.Stats.ViewCount, article.Stats.FavoriteCount, article.Stats.LikeCount, article.Stats.ReplyCount, article.Stats.ShareCount, article.Stats.CoinCount);
        var info = new ArticleInformation(identifier, user, publishTime, communityInfo);
        info.AddExtensionIfNotNull(ArticleExtensionDataId.WordCount, article.WordCount);
        info.AddExtensionIfNotNull(ArticleExtensionDataId.Partition, article.Category.ToPartition());
        info.AddExtensionIfNotNull(ArticleExtensionDataId.RelatedPartitions, article.RelatedCategories?.Select(p => p.ToPartition()).ToList());
        return info;
    }

    /// <summary>
    /// 判断阅读页返回的内容状态是否足以映射成文章.
    /// </summary>
    /// <remarks>
    /// 阅读页偶尔会返回结构完整但 <c>readInfo</c> 为空、或缺少作者与正文的状态。
    /// 这类响应不能直接当作最终结果，否则同一篇文章会在重试后本可成功时被判为失败。
    /// 这里复用最终映射的正文生成与格式化规则，避免重试判断与
    /// <see cref="ToArticleDetail"/> 对“正文是否可显示”得出不同结论。
    /// </remarks>
    internal static bool HasUsableContent(ArticleContentResponse? response)
    {
        var readInfo = response?.readInfo;
        return readInfo?.author is not null
            && !string.IsNullOrWhiteSpace(GetRenderableContent(readInfo));
    }

    /// <summary>
    /// 按最终展示规则生成正文 HTML：存在 opus 时忽略旧 content，再统一格式化.
    /// </summary>
    private static string GetRenderableContent(Readinfo readInfo)
    {
        var rawContent = readInfo.opus is null ? readInfo.content : BuildHtmlContent(readInfo.opus);
        return FormatHtmlContent(rawContent);
    }

    public static ArticleDetail ToArticleDetail(this ArticleContentResponse response, ArticleIdentifier identifier, ArticleViewResponse? view)
    {
        var readInfo = response?.readInfo ?? throw new KernelException("文章内容数据为空，无法解析该文章.");
        var author = readInfo.author ?? throw new KernelException("文章作者信息缺失，无法解析该文章.");
        // 统计信息属于可选数据，缺失时保留为空而不是中断整篇文章.
        var stats = view?.stats;
        var user = UserAdapterBase.CreateUserProfile(author.mid, author.name, author.face, 96d);
        var detailProfile = new UserDetailProfile(user, default, author.level, author.vip?.type > 0);
        var publishTime = DateTimeOffset.FromUnixTimeSeconds(readInfo.publish_time).ToLocalTime();
        var communityInfo = new ArticleCommunityInformation(identifier.Id, stats?.view, stats?.favorite, stats?.like, stats?.reply, stats?.share, stats?.coin);
        var tags = readInfo.tags?.Select(p => new BiliTag(p.tid.ToString(), p.name)).ToList();
        bool? isLiked = view is null ? null : view.like > 0;
        var isFav = view?.favorite;
        var relation = view?.attention == true ? UserRelationStatus.Following : UserRelationStatus.Unfollow;
        var userCard = new UserCard(detailProfile, new UserCommunityInformation(author.mid.ToString(), fansCount: author.fans, relation: relation));
        var content = GetRenderableContent(readInfo);
        if (string.IsNullOrWhiteSpace(content))
        {
            // 正文为空或只有不受支持的节点时不能当作成功返回，否则阅读器会显示空白文章.
            throw new KernelException("文章正文为空或格式不受支持，无法显示该文章.");
        }

        return new ArticleDetail(identifier, userCard, communityInfo, content, publishTime, tags, isLiked, isFav);
    }

    private static string FormatHtmlContent(string? content)
        => (content ?? string.Empty).Replace("data-src", "src").Replace("\"//", "\"http://").Replace("<p></p>", string.Empty).Replace("<p><br></p>", string.Empty).Replace("<p><br/></p>", string.Empty);

    private static string BuildHtmlContent(Opus opus)
    {
        var paragraphs = opus.content?.paragraphs;
        if (paragraphs is null)
        {
            return string.Empty;
        }

        var htmlContent = string.Empty;
        foreach (var item in paragraphs)
        {
            if (item is null)
            {
                continue;
            }

            if (item.para_type == 1)
            {
                // 文本节点.
                var nodes = item.text?.nodes;
                if (nodes is null)
                {
                    // 空文本段落没有可展示内容，跳过.
                    continue;
                }

                var text = string.Empty;
                foreach (var node in nodes)
                {
                    if (node?.word is null || string.IsNullOrWhiteSpace(node.word.words))
                    {
                        // 没有文本内容的节点（例如被裁剪的 opus 数据）跳过，避免中断整篇文章.
                        continue;
                    }

                    if (node.node_type == 1)
                    {
                        if (node.word.words == "\\n")
                        {
                            continue;
                        }

                        var fontsize = node.word.font_size == 0 ? 17 : node.word.font_size;
                        var innerText = node.word.style?.bold == true
                            ? $"<strong>{WebUtility.HtmlEncode(node.word.words)}</strong>"
                            : WebUtility.HtmlEncode(node.word.words);
                        text += $"<p style=\"font-size:'{fontsize}'\">{innerText}</p>";
                    }
                    else
                    {
                        text += $"<p>{WebUtility.HtmlEncode(node.word.words)}</p>";
                    }
                }

                htmlContent += $"<p>{text}</p>";
            }
            else if (item.para_type == 2)
            {
                // 图片节点.
                var pics = item.pic?.pics;
                if (pics is null)
                {
                    continue;
                }

                foreach (var img in pics)
                {
                    if (img is null || string.IsNullOrEmpty(img.url))
                    {
                        continue;
                    }

                    var imgTag = $"""
                        <p class="image-package">
                           <figure class="img-box" contenteditable="false">
                              <img src="{img.url}" width="{img.width}" height="{img.height}" data-size="{img.size}"/>
                           </figure>
                        </p>
                        """;

                    htmlContent += imgTag;
                }
            }
            else if (item.para_type == 3)
            {
                // 分割线.
                var line = item.line?.pic;
                if (line is null || string.IsNullOrEmpty(line.url))
                {
                    continue;
                }

                var imgTag = $"""
                    <img src="{line.url}" style="max-width: 100%; width: auto; display: block; margin: 0 auto;" height="{line.height}" />
                    """;
                htmlContent += imgTag;
            }
        }

        return htmlContent;
    }
}
