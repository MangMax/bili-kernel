using System.Text.Json;
using Richasy.BiliKernel;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Services.Article.Core;
using Richasy.BiliKernel.Services.Article.Core.Models;
using RichasyKernel;
using Xunit;
namespace Services.Article.Tests;
public class ReviewCounterexamples
{
    private static ArticleDetail Map(string paragraphs) {
        var json = "{\"readInfo\":{\"author\":{\"mid\":42,\"name\":\"Synthetic\",\"face\":\"https://example.invalid/a.png\"},\"opus\":{\"content\":{\"paragraphs\":" + paragraphs + "}}}}";
        var response = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.ArticleContentResponse)!;
        return response.ToArticleDetail(new ArticleIdentifier("1", "Synthetic", null, null), null!);
    }
    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public void EmptyWordMustNotReturnSuccessfulBody(string words) {
        Assert.Throws<KernelException>(() => Map("[{\"para_type\":1,\"text\":{\"nodes\":[{\"node_type\":1,\"word\":{\"words\":" + words + "}}]}}]"));
    }
    [Fact]
    public void NullItemsAndMissingMediaPreserveEncodedText() {
        var detail = Map("[null,{\"para_type\":2,\"pic\":{\"pics\":[null,{\"url\":null}]}},{\"para_type\":99},{\"para_type\":1,\"text\":{\"nodes\":[null,{\"node_type\":1,\"word\":{\"words\":\"<&>\"}}]}}]");
        Assert.Equal("<p><p style=\"font-size:'17'\">&lt;&amp;&gt;</p></p>", detail.HtmlContent);
    }
}
