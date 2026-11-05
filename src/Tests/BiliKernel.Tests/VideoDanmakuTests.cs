using Bilibili.Community.Service.Dm.V1;
using Google.Protobuf;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Services.Media;
using RichasyKernel;
using Xunit;

namespace Richasy.BiliKernel.Tests;

public sealed class VideoDanmakuTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task WebReadsPreserveProtobufAndExcludeAppCredentials(bool cookie, bool token)
    {
        var (handler, service) = Create(cookie, token, r => r.Path.EndsWith("view")
            ? Proto(new DmWebViewReply { Count = 19, DmSge = new DmSegConfig { Total = 3 } })
            : Segment());
        var meta = await service.GetDanmakuMetaAsync("42", "84");
        Assert.Equal(19, meta.Total);
        Assert.Equal(3, meta.SegmentCount);
        var items = await service.GetSegmentDanmakusWithWebAsync("42", "84", 2);
        var item = Assert.Single(items);
        Assert.Equal("123", item.Id);
        Assert.Equal("hello 弹幕", item.Content);
        Assert.Equal(1.25, item.StartPosition);
        Assert.Equal(5, item.Mode);
        Assert.Equal(25, item.FontSize);
        Assert.Equal(0xff00u, item.Color);
        Assert.Equal("2", handler.Requests.Last().Query["segment_index"]);
        Assert.Equal(2, handler.Requests.Count(r => r.Path.Contains("/x/v2/dm/")));
        foreach (var r in handler.Requests.Where(r => r.Path.Contains("/x/v2/dm/")))
        {
            Assert.Equal("42", r.Query["pid"]);
            Assert.Equal("84", r.Query["oid"]);
            Assert.Equal("1", r.Query["type"]);
            Assert.False(r.Query.ContainsKey("access_key"));
            Assert.False(r.Query.ContainsKey("w_rid"));
            Assert.False(r.Query.ContainsKey("csrf"));
            Assert.Equal(cookie, r.Cookie?.Contains("SESSDATA=session") == true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DenialPropagatesAndSegmentRetriesAreBounded(bool segment)
    {
        var (handler, service) = Create(true, true, _ => RecordingHttpMessageHandler.Json("{\"code\":-101,\"message\":\"denied\"}"));
        await Assert.ThrowsAsync<KernelException>(async () =>
        {
            if (segment) await service.GetSegmentDanmakusWithWebAsync("42", "84", 1).ConfigureAwait(false);
            else await service.GetDanmakuMetaAsync("42", "84").ConfigureAwait(false);
        });
        Assert.Equal(segment ? 3 : 1, handler.Requests.Count(r => r.Path.Contains("/x/v2/dm/")));
        Assert.All(handler.Requests.Where(r => r.Path.Contains("/x/v2/dm/")), r => Assert.False(r.Query.ContainsKey("access_key")));
    }

    [Fact]
    public async Task TransientFailureRecoversWithinRetryBound()
    {
        var attempts = 0;
        var (_, service) = Create(true, true, _ => ++attempts < 3
            ? throw new HttpRequestException("temporary") : Segment());
        Assert.Equal("hello 弹幕", Assert.Single(await service.GetSegmentDanmakusWithWebAsync("42", "84", 3)).Content);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task CancellationDuringRequestStopsRetries()
    {
        using var cts = new CancellationTokenSource();
        var (handler, service) = Create(true, true, _ => { cts.Cancel(); throw new OperationCanceledException(cts.Token); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetSegmentDanmakusWithWebAsync("42", "84", 1, cts.Token));
        Assert.Single(handler.Requests, r => r.Path.Contains("/x/v2/dm/"));
    }

    [Fact]
    public async Task PreCanceledReadSendsNoRequests()
    {
        var (handler, service) = Create(true, true, _ => Segment());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetSegmentDanmakusWithWebAsync("42", "84", 1, new CancellationToken(true)));
        Assert.Empty(handler.Requests);
    }

    private static HttpResponseMessage Segment() => Proto(new DmSegMobileReply
    {
        Elems = { new DanmakuElem { Id = 123, Content = "hello 弹幕", Progress = 1250, Mode = 5, Color = (DmColorfulType)0xff00, Fontsize = 25 } },
    });

    private static HttpResponseMessage Proto(IMessage message)
    {
        var content = new ByteArrayContent(message.ToByteArray());
        content.Headers.ContentType = new("application/octet-stream");
        return new(System.Net.HttpStatusCode.OK) { Content = content };
    }

    private static (RecordingHttpMessageHandler, DanmakuService) Create(bool cookie, bool token, Func<RecordedRequest, HttpResponseMessage> respond)
    {
        var handler = new RecordingHttpMessageHandler(respond).With("gaia-gateway", "{\"code\":0}");
        var cookies = new FakeCookiesResolver();
        if (cookie) cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session" });
        var tokens = new FakeTokenResolver { Token = token ? new BiliToken { AccessToken = "app-token" } : null };
        var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), handler);
        var http = new BiliHttpClient(handler, buvid);
        return (handler, new DanmakuService(http, new BiliAuthenticator(cookies, tokens, buvid, http), tokens));
    }
}



