// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Xunit;
using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using Bilibili.App.Archive.V1;
using Bilibili.App.Card.V1;
using Bilibili.App.Show.Popular.V1;
using Bilibili.App.View.V1;
using Google.Protobuf;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media;

namespace Richasy.BiliKernel.Tests;

public sealed class GrpcCardCategoryTests
{
    [Theory]
    [InlineData("话题", "话题", 12)]
    [InlineData("", "科技", 36)]
    public async Task HotFeedMapsCategoryFromExistingGrpcArgs(string tag, string expected, long id)
    {
        var reply = new PopularReply();
        reply.Items.Add(new Card { SmallCoverV5 = new SmallCoverV5
        {
            Base = new Base
            {
                CardGoto = "av", Uri = "bilibili://video/123", Title = "Video", Cover = "https://example.com/cover.jpg", Idx = 9,
                Args = new Args { Tid = 12, Tname = tag, Rid = 36, Rname = "科技" },
                ThreePointV4 = new ThreePointV4 { SharePlane = new SharePlane { Aid = 123, AuthorId = 42, Author = "作者", PlayNumber = "1万次" } },
            },
            CoverRightText1 = "01:00", RightDesc2 = "作者·昨天",
        }});
        using var handler = new GrpcHandler(reply);
        using var client = new BiliHttpClient(handler);
        var service = new VideoDiscoveryService(client, new BiliAuthenticator(null, null), new NoToken());
        var (items, offset) = await service.GetHotVideoListAsync();
        var video = Assert.Single(items);
        Assert.Equal(expected, video.GetExtensionIfNotNull<string>(VideoExtensionDataId.TagName));
        Assert.Equal(id, video.GetExtensionIfNotNull<long?>(VideoExtensionDataId.TagId));
        Assert.Equal(9, offset);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task VideoDetailsUseArchiveClassificationWhenTagIsAbsent()
    {
        var reply = new ViewReply
        {
            Arc = new Arc
            {
                Aid = 123, Title = "Video", Pic = "https://example.com/cover.jpg", Pubdate = 1700000000,
                TypeId = 36, TypeName = "科技", Author = new Author { Mid = 42, Name = "作者", Face = "https://example.com/avatar.jpg" },
                Stat = new Stat(), Dimension = new Bilibili.App.Archive.V1.Dimension { Width = 1920, Height = 1080 },
            },
            ReqUser = new ReqUser(),
        };
        using var handler = new GrpcHandler(reply);
        using var client = new BiliHttpClient(handler);
        var service = new PlayerService(client, new BiliAuthenticator(null, null), new NoToken());
        var view = await service.GetVideoPageDetailAsync(new("123", "Video", null));
        Assert.Equal("科技", view.Information.GetExtensionIfNotNull<string>(VideoExtensionDataId.TagName));
        Assert.Equal(36, view.Information.GetExtensionIfNotNull<long?>(VideoExtensionDataId.TagId));
        Assert.Equal(1, handler.Calls);
    }

    private sealed class NoToken : IBiliTokenResolver
    {
        public BiliToken? GetToken() => null;
        public void SaveToken(BiliToken token) => throw new NotSupportedException();
        public void RemoveToken() => throw new NotSupportedException();
    }

    private sealed class GrpcHandler(IMessage payload) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var bytes = payload.ToByteArray();
            var frame = new byte[bytes.Length + 5];
            BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(1, 4), bytes.Length);
            bytes.CopyTo(frame, 5);
            var content = new ByteArrayContent(frame);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
