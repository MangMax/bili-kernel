using Bilibili.App.Interfaces.V1;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Services.User;
using Xunit;

namespace Richasy.BiliKernel.Tests;

public sealed class HistoryWriteTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteUsesCookieOnlyAndSelectedRecord(bool hasToken)
    {
        var handler = new RecordingHttpMessageHandler();
        var service = Create(handler, hasToken);
        var video = new VideoInformation(new MediaIdentifier("42", "selected", null), new PublisherProfile(new UserProfile("1")), 10);
        await service.RemoveVideoHistoryItemAsync(video);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/x/v2/history/delete", request.Path);
        Assert.Contains("kid=archive_42", request.Body);
        Assert.Contains("csrf=csrf-token", request.Body);
        Assert.Contains("SESSDATA=session", request.Cookie);
        Assert.DoesNotContain("access_key", request.Body);
        Assert.False(request.Query.ContainsKey("access_key"));
    }

    [Theory]
    [InlineData(ViewHistoryTabType.Video, "archive")]
    [InlineData(ViewHistoryTabType.Live, "live")]
    [InlineData(ViewHistoryTabType.Article, "article")]
    public async Task CategoryClearPreservesBusiness(ViewHistoryTabType tab, string business)
    {
        var handler = new RecordingHttpMessageHandler(_ => GrpcSuccess());
        await Create(handler).CleanHistoryAsync(tab);
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("History/Clear", request.Path);
        var message = ClearReq.Parser.ParseFrom(System.Text.Encoding.UTF8.GetBytes(request.Body!)[5..]);
        Assert.Equal(business, message.Business);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WebRejectionFallsBackAndRetainsFailure(bool fallbackSucceeds)
    {
        var handler = new RecordingHttpMessageHandler(request => request.Path.Contains("History/Clear") && fallbackSucceeds
            ? GrpcSuccess() : RecordingHttpMessageHandler.Json("{\"code\":-101,\"message\":\"denied\"}"));
        var service = Create(handler);
        if (fallbackSucceeds)
            await service.CleanHistoryAsync(ViewHistoryTabType.All);
        else
            await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => service.CleanHistoryAsync(ViewHistoryTabType.All));
        Assert.Collection(handler.Requests,
            web => { Assert.Equal("/x/v2/history/clear", web.Path); Assert.DoesNotContain("access_key", web.Body); },
            grpc => Assert.EndsWith("History/Clear", grpc.Path));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteFallbackPreservesSelectedRecord(bool succeeds)
    {
        var handler = new RecordingHttpMessageHandler(request => request.Path.Contains("History/Delete") && succeeds
            ? GrpcSuccess() : RecordingHttpMessageHandler.Json("{\"code\":-101}"));
        var service = Create(handler);
        var video = new VideoInformation(new MediaIdentifier("42", "selected", null), new PublisherProfile(new UserProfile("1")), 10);
        if (succeeds)
            await service.RemoveVideoHistoryItemAsync(video);
        else
            await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => service.RemoveVideoHistoryItemAsync(video));
        var request = Assert.Single(handler.Requests, p => p.Path.EndsWith("History/Delete"));
        var message = DeleteReq.Parser.ParseFrom(System.Text.Encoding.UTF8.GetBytes(request.Body!)[5..]);
        var selected = Assert.Single(message.HisInfo);
        Assert.Equal("archive", selected.Business);
        Assert.Equal(42, selected.Kid);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingOrExpiredCookieRemainsServerRejection(bool hasCookie)
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("{\"code\":-101}"));
        await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => Create(handler, hasCookie: hasCookie).CleanHistoryAsync(ViewHistoryTabType.All));
        var web = handler.Requests[0];
        Assert.Equal("/x/v2/history/clear", web.Path);
        Assert.DoesNotContain("access_key", web.Body);
        if (hasCookie) Assert.Contains("SESSDATA=session", web.Cookie);
        else Assert.True(string.IsNullOrEmpty(web.Cookie));
    }

    private static System.Net.Http.HttpResponseMessage GrpcSuccess()
    {
        var response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.ByteArrayContent(new byte[5]) };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/grpc");
        response.Headers.Add("grpc-status", "0");
        return response;
    }

    private static ViewHistoryService Create(RecordingHttpMessageHandler handler, bool hasToken = true, bool hasCookie = true)
    {
        var cookies = new FakeCookiesResolver();
        if (hasCookie) cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "session", ["bili_jct"] = "csrf-token" });
        var http = new BiliHttpClient(handler);
        var tokens = new FakeTokenResolver { Token = hasToken ? new BiliToken { AccessToken = "app-token" } : null };
        return new ViewHistoryService(http, new FakeAuthenticationService(), new BiliAuthenticator(cookies, tokens, httpClient: http));
    }
}

