// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Services.Article.Core;
using Richasy.BiliKernel.Services.Article.Core.Models;
using RichasyKernel;
using System.Text.Json;
using Xunit;

namespace Services.Article.Tests;

/// <summary>
/// Exercises the real article adapter with desensitized structured fixtures.
/// The fixtures use only synthetic identifiers, text and example.invalid hosts.
/// </summary>
public sealed class ArticleAdapterTests
{
    private static readonly ArticleIdentifier DefaultIdentifier = new("1001", "Synthetic article", null, null);

    [Fact]
    public void LegacyHtmlContentKeepsParagraphsLinksAndImages()
    {
        var response = LoadContent("legacy-content.json");
        var view = LoadView("view-stats.json");

        var detail = response.ToArticleDetail(DefaultIdentifier, view);

        Assert.Equal(
            "<p>first paragraph</p><p><a href=\"http://example.invalid/link\">link text</a></p><figure><img src=\"http://example.invalid/image.png\"/></figure>",
            detail.HtmlContent);
        Assert.Equal("Author42", detail.Author.Profile!.User.Name);
        Assert.Equal(3, detail.Author.Profile.Level);
        Assert.True(detail.Author.Profile.IsVip);
        Assert.Equal("42", detail.Author.Community!.Id);
        Assert.Equal(7, detail.Author.Community.FansCount);
        Assert.Equal(UserRelationStatus.Following, detail.Author.Community.Relation);
        Assert.Equal(10, detail.CommunityInformation.ViewCount);
        Assert.Equal(2, detail.CommunityInformation.FavoriteCount);
        Assert.Equal(3, detail.CommunityInformation.LikeCount);
        Assert.Equal(4, detail.CommunityInformation.CommentCount);
        Assert.Equal(5, detail.CommunityInformation.ShareCount);
        Assert.Equal(6, detail.CommunityInformation.CoinCount);
        Assert.True(detail.IsLiked);
        Assert.True(detail.IsFavorited);
        Assert.Equal(1700000000, detail.PublishTime.ToUnixTimeSeconds());
        var tag = Assert.Single(detail.Tags);
        Assert.Equal("9", tag.Id);
        Assert.Equal("synthetic-tag", tag.Name);
    }

    [Fact]
    public void OpusContentKeepsTextParagraphImageAndLineSemantics()
    {
        var response = LoadContent("opus-content.json");
        var view = LoadView("view-stats.json");

        var detail = response.ToArticleDetail(new ArticleIdentifier("1002", "Synthetic article", null, null), view);

        var html = detail.HtmlContent;
        Assert.Contains("<p style=\"font-size:'17'\"><strong>bold</strong></p>", html);
        Assert.Contains("<p style=\"font-size:'20'\">plain</p>", html);
        Assert.Contains("<p class=\"image-package\">", html);
        Assert.Contains("<figure class=\"img-box\" contenteditable=\"false\">", html);
        Assert.Contains("<img src=\"https://example.invalid/img.png\" width=\"640\" height=\"480\" data-size=\"2\"/>", html);
        Assert.Contains(
            "<img src=\"https://example.invalid/line.png\" style=\"max-width: 100%; width: auto; display: block; margin: 0 auto;\" height=\"20\" />",
            html);
        Assert.DoesNotContain("\\n", html);
        Assert.True(html.IndexOf("bold", StringComparison.Ordinal) < html.IndexOf("img.png", StringComparison.Ordinal));
        Assert.True(html.IndexOf("img.png", StringComparison.Ordinal) < html.IndexOf("line.png", StringComparison.Ordinal));
        Assert.Null(detail.Tags);
    }

    [Fact]
    public void MissingOptionalStatsDoNotFailAndStayEmpty()
    {
        var response = LoadContent("legacy-content.json");
        var view = LoadView("view-no-stats.json");

        var detail = response.ToArticleDetail(DefaultIdentifier, view);

        Assert.Null(detail.CommunityInformation.ViewCount);
        Assert.Null(detail.CommunityInformation.FavoriteCount);
        Assert.Null(detail.CommunityInformation.LikeCount);
        Assert.Null(detail.CommunityInformation.CommentCount);
        Assert.Null(detail.CommunityInformation.ShareCount);
        Assert.Null(detail.CommunityInformation.CoinCount);
        Assert.False(detail.IsLiked);
        Assert.False(detail.IsFavorited);
        Assert.Equal(UserRelationStatus.Unfollow, detail.Author.Community!.Relation);
        Assert.Equal("<p>first paragraph</p><p><a href=\"http://example.invalid/link\">link text</a></p><figure><img src=\"http://example.invalid/image.png\"/></figure>", detail.HtmlContent);
    }

    [Fact]
    public void MissingMetadataObjectStillRendersBody()
    {
        var response = LoadContent("legacy-content.json");

        var detail = response.ToArticleDetail(DefaultIdentifier, null!);

        Assert.Null(detail.CommunityInformation.ViewCount);
        Assert.Null(detail.IsLiked);
        Assert.Null(detail.IsFavorited);
        Assert.Equal(UserRelationStatus.Unfollow, detail.Author.Community!.Relation);
        Assert.NotEmpty(detail.HtmlContent);
    }

    [Fact]
    public void EmptyTextNodesAreSkippedWithoutFailing()
    {
        var response = LoadContent("partial-empty-text.json");

        var detail = response.ToArticleDetail(DefaultIdentifier, LoadView("view-stats.json"));

        Assert.Equal("<p><p style=\"font-size:'17'\">kept</p></p>", detail.HtmlContent);
    }

    [Fact]
    public void PayloadlessMediaIsSkippedWithoutFailing()
    {
        var response = LoadContent("partial-payloadless-media.json");

        var detail = response.ToArticleDetail(DefaultIdentifier, LoadView("view-stats.json"));

        Assert.Equal("<p><p style=\"font-size:'17'\">visible</p></p>", detail.HtmlContent);
    }

    [Fact]
    public void UnsupportedParagraphTypeIsIgnoredWhenSupportedContentExists()
    {
        var response = LoadContent("unsupported-mixed.json");

        var detail = response.ToArticleDetail(DefaultIdentifier, LoadView("view-stats.json"));

        Assert.Equal("<p><p style=\"font-size:'17'\">kept</p></p>", detail.HtmlContent);
    }

    [Fact]
    public void MissingContentDataFailsWithIdentifiableError()
    {
        var response = LoadContent("invalid-missing-content.json");

        var error = Assert.Throws<KernelException>(() => response.ToArticleDetail(DefaultIdentifier, LoadView("view-stats.json")));

        Assert.Contains("文章内容数据为空", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingAuthorFailsWithIdentifiableError()
    {
        var response = LoadContent("invalid-missing-author.json");

        var error = Assert.Throws<KernelException>(() => response.ToArticleDetail(DefaultIdentifier, LoadView("view-stats.json")));

        Assert.Contains("文章作者信息缺失", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("invalid-empty-body.json")]
    [InlineData("invalid-opus-without-paragraphs.json")]
    [InlineData("invalid-unsupported-only.json")]
    public void EmptyOrUnsupportedBodyFailsInsteadOfReturningEmptySuccess(string fixture)
    {
        var response = LoadContent(fixture);

        var error = Assert.Throws<KernelException>(() => response.ToArticleDetail(DefaultIdentifier, LoadView("view-stats.json")));

        Assert.Contains("文章正文为空或格式不受支持", error.Message, StringComparison.Ordinal);
    }

    private static Readinfo? LoadContent(string name)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
        return JsonSerializer.Deserialize(json, SourceGenerationContext.Default.Readinfo);
    }

    private static ArticleViewResponse LoadView(string name)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
        return JsonSerializer.Deserialize(json, SourceGenerationContext.Default.ArticleViewResponse)
            ?? throw new InvalidOperationException($"Fixture '{name}' could not be deserialized.");
    }
}
