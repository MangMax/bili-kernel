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

public sealed class WebFeedCategoryTests
{
    [Theory]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData("null", null)]
    [InlineData("99", null)]
    public async Task WebFeedMapsClassificationAndNumericFollowingWithoutGuessing(string following, bool? expected)
    {
        using var handler = new Handler(following);
        using var client = new BiliHttpClient(handler);
        var service = new VideoDiscoveryService(client, new BiliAuthenticator(null, null, httpClient: client), null!);
        var (items, _) = await service.GetCuratedFeedAsync();
        var video = Assert.Single(items);
        Assert.Equal("科技", video.GetExtensionIfNotNull<string>(VideoExtensionDataId.TagName));
        Assert.Equal(36, video.GetExtensionIfNotNull<long?>(VideoExtensionDataId.TagId));
        Assert.Equal(expected, video.GetExtensionIfNotNull<bool?>(VideoExtensionDataId.PublisherIsFollowed));
    }

    private sealed class Handler(string following) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = request.RequestUri!.AbsolutePath switch
            {
                "/x/web-interface/nav" => """{"code":0,"data":{"wbi_img":{"img_url":"https://example.com/7cd084941338484aae1ad9425b84077c.png","sub_url":"https://example.com/4932caff0ff746eab6f01bf08b70ac45.png"}}}""",
                "/x/web-interface/wbi/index/top/feed/rcmd" => """
                    {"code":0,"data":{"item":[{"id":123,"goto":"av","pic":"https://example.com/cover.jpg",
                    "title":"Video","duration":60,"pubdate":1700000000,"tid":36,"tname":"科技","is_followed":FOLLOWING,
                    "owner":{"mid":42,"name":"作者","face":"https://example.com/avatar.jpg"},"stat":{"view":100,"danmaku":10}}]}}
                    """.Replace("FOLLOWING", following),
                _ => throw new InvalidOperationException("Unexpected endpoint: " + request.RequestUri.AbsolutePath),
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
