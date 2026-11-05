// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Xunit;
using System.Net;
using System.Text;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media;

namespace Richasy.BiliKernel.Tests;

public sealed class RecommendFeedMappingTests
{
    [Fact]
    public async Task FeedSuppliesAvatarTimeAndExplicitFollowingWithoutAuthorRequests()
    {
        using var handler = new FeedHandler("""
            {"mask":{"avatar":{"cover":"https://example.com/mask.jpg"}},
             "avatar":{"cover":"https://example.com/avatar.jpg"},"is_atten":false,
             "bottom_rcmd_reason":"已关注","pubdate":1700000000,
             "multiply_desc":{"extra":"作者 · 2小时前"}}
            """);
        using var client = new BiliHttpClient(handler);
        var service = new VideoDiscoveryService(client, new BiliAuthenticator(null, null), null!);
        var (items, offset) = await service.GetRecommendVideoListAsync();
        var video = Assert.Single(items);
        Assert.Contains("mask.jpg", video.Publisher.User.Avatar!.Uri.ToString());
        Assert.False(video.GetExtensionIfNotNull<bool?>(VideoExtensionDataId.PublisherIsFollowed));
        Assert.Equal(1700000000, video.PublishTime!.Value.ToUnixTimeSeconds());
        Assert.Equal("2小时前", video.GetExtensionIfNotNull<string>(VideoExtensionDataId.Subtitle));
        Assert.Equal(9, offset);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("{\"avatar\":{\"cover\":\"https://example.com/avatar.jpg\"},\"bottom_rcmd_reason\":\"已关注\",\"desc\":\"作者 · 昨天\"}", true)]
    [InlineData("{}", null)]
    public async Task OptionalFeedFieldsPreserveUnknownAndUseFallbacks(string fields, bool? following)
    {
        using var handler = new FeedHandler(fields);
        using var client = new BiliHttpClient(handler);
        var service = new VideoDiscoveryService(client, new BiliAuthenticator(null, null), null!);
        var (items, _) = await service.GetRecommendVideoListAsync();
        var video = Assert.Single(items);
        Assert.Equal(following, video.GetExtensionIfNotNull<bool?>(VideoExtensionDataId.PublisherIsFollowed));
        Assert.Null(video.PublishTime);
        if (following is true)
        {
            Assert.Contains("avatar.jpg", video.Publisher.User.Avatar!.Uri.ToString());
            Assert.Equal("昨天", video.GetExtensionIfNotNull<string>(VideoExtensionDataId.Subtitle));
        }
        else
        {
            Assert.Null(video.Publisher.User.Avatar);
            Assert.Null(video.GetExtensionIfNotNull<string>(VideoExtensionDataId.Subtitle));
        }
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("\"tid\":12,\"tname\":\"标签\",\"rid\":36,\"rname\":\"科技\"", "标签", 12L)]
    [InlineData("\"rid\":36,\"rname\":\"科技\"", "科技", 36L)]
    [InlineData("\"tname\":\"  \",\"rid\":36,\"rname\":\" 科技 \"", "科技", 36L)]
    [InlineData("\"rid\":36", null, null)]
    public async Task MobileCategoryUsesTagThenRegionAndDoesNotInventMissingLabels(string fields, string? expected, long? expectedId)
    {
        using var handler = new FeedHandler("{}", "{\"aid\":123,\"up_id\":42,\"up_name\":\"作者\"," + fields + "}");
        using var client = new BiliHttpClient(handler);
        var service = new VideoDiscoveryService(client, new BiliAuthenticator(null, null), null!);
        var (items, _) = await service.GetRecommendVideoListAsync();
        var video = Assert.Single(items);
        Assert.Equal(expected, video.GetExtensionIfNotNull<string>(VideoExtensionDataId.TagName));
        Assert.Equal(expectedId, video.GetExtensionIfNotNull<long?>(VideoExtensionDataId.TagId));
    }

    private sealed class FeedHandler(string fields, string? args = null) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Contains("/x/v2/feed/index", request.RequestUri!.AbsolutePath);
            var json = """
                {"code":0,"data":{"items":[{"card_goto":"av","idx":9,"title":"Video",
                "cover":"https://example.com/cover.jpg","args":ARGS,
                "cover_right_text":"01:00","cover_left_text_1":"1万","cover_left_text_2":"100",FIELDS}]}}
                """.Replace("FIELDS", fields.Trim()[1..^1])
                .Replace("ARGS", args ?? "{\"aid\":123,\"up_id\":42,\"up_name\":\"作者\"}");
            // An empty optional object should not leave a trailing comma.
            json = json.Replace(",}]", "}]");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
