// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media;
using RichasyKernel;
using Xunit;

namespace Richasy.BiliKernel.Tests;

public sealed class VideoCardInformationTests
{
    [Fact]
    public async Task PublicMetadataMapsCollaboratorsWithoutPlaybackRequests()
    {
        var (handler, http, service) = Create("""
            {"code":0,"data":{"View":{"aid":42,"bvid":"BV17x411w7KC","title":"collaboration","pic":"https://example.com/cover.jpg","pubdate":1700000000,
            "owner":{"mid":42,"name":"Owner","face":"https://example.com/owner.jpg"},"rights":{"is_cooperation":1},
            "staff":[{"mid":42,"name":"Owner","face":"https://example.com/owner.jpg","title":"UP主"},{"mid":43,"name":"Guest","face":"https://example.com/guest.jpg","title":"参演"}]}}}
            """);
        using (handler) using (http)
        {
            var info = await service.GetVideoInformationAsync(new("42", "", null));
            Assert.Equal("42", info.Publisher.User.Id);
            Assert.Equal(2, info.Collaborators!.Count);
            Assert.Equal("43", info.Collaborators[1].User.Id);
            Assert.Equal("参演", info.Collaborators[1].Role);
            Assert.True(info.GetExtensionIfNotNull<bool>(VideoExtensionDataId.IsCollaborativeSubmission));
            Assert.False(info.GetExtensionIfNotNull<bool>(VideoExtensionDataId.IsChargingExclusive));
            var request = Assert.Single(handler.Requests, r => r.Path == "/x/web-interface/view/detail");
            Assert.Contains("SESSDATA=synthetic-session", request.Cookie);
            Assert.True(request.Query.ContainsKey("w_rid"));
            Assert.False(request.Query.ContainsKey("access_key"));
            Assert.All(handler.Requests, r => Assert.True(r.Path is "/x/web-interface/nav" or "/x/web-interface/view/detail" or "/x/internal/gaia-gateway/ExClimbWuzhi", r.Path));
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ExclusiveClassificationDoesNotDependOnCurrentViewingEntitlement(bool canPlay, bool preview)
    {
        var (handler, http, service) = Create("""
            {"code":0,"data":{"View":{"aid":42,"title":"exclusive","pic":"https://example.com/cover.jpg","owner":{"mid":42,"name":"Owner","face":"https://example.com/owner.jpg"},
            "is_upower_exclusive":true,"is_upower_play":CAN_PLAY,"is_upower_preview":PREVIEW}}}
            """.Replace("CAN_PLAY", canPlay ? "true" : "false").Replace("PREVIEW", preview ? "true" : "false"));
        using (handler) using (http)
        {
            var info = await service.GetVideoInformationAsync(new("42", "", null));
            Assert.True(info.GetExtensionIfNotNull<bool>(VideoExtensionDataId.IsChargingExclusive));
            Assert.False(info.GetExtensionIfNotNull<bool>(VideoExtensionDataId.IsCollaborativeSubmission));
            Assert.Null(info.Collaborators);
        }
    }

    [Fact]
    public async Task MissingMetadataIsAnErrorRatherThanAnOrdinaryVideo()
    {
        var (handler, http, service) = Create("{\"code\":0,\"data\":null}");
        using (handler) using (http)
            await Assert.ThrowsAsync<KernelException>(() => service.GetVideoInformationAsync(new("42", "", null)));
    }

    private static (RecordingHttpMessageHandler, BiliHttpClient, PlayerService) Create(string json)
    {
        var handler = new RecordingHttpMessageHandler()
            .With("/x/web-interface/nav", """{"code":0,"data":{"wbi_img":{"img_url":"https://i0.hdslb.com/0123456789abcdef0123456789abcdef.png","sub_url":"https://i0.hdslb.com/fedcba9876543210fedcba9876543210.png"}}}""")
            .With("/x/web-interface/view/detail", json.Replace("\"owner\":", "\"stat\":{},\"owner\":"));
        var cookies = new FakeCookiesResolver();
        cookies.SaveCookies(new Dictionary<string,string> { ["SESSDATA"] = "synthetic-session" });
        var tokens = new FakeTokenResolver { Token = new BiliToken { AccessToken = "synthetic-app-token" } };
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        return (handler, http, new PlayerService(http, new BiliAuthenticator(cookies, tokens, buvid, http), tokens));
    }
}
