using System.Net;
using Richasy.BiliKernel.Http;
using Xunit;

namespace Services.Article.Tests;

public sealed class IndependentHttpBoundaryTests
{
    [Fact]
    public void OriginalTwoParameterSignatureIsRetained()
        => Assert.NotNull(typeof(BiliHttpClient).GetMethod("GetStringAsync", [typeof(HttpRequestMessage), typeof(CancellationToken)]));

    [Fact]
    public async Task ForbiddenResponseAndContentAreDisposedBeforeClientDisposal()
    {
        var content = new TrackedContent();
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = content };
        using var client = new BiliHttpClient(new Handler(response));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid");
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStringAsync(request, default, ensureSuccess: true));
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task DefaultStillReturnsNonSuccessBody()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("synthetic denied") };
        using var client = new BiliHttpClient(new Handler(response));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid");
        Assert.Equal("synthetic denied", await client.GetStringAsync(request, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulResponseContentIsDisposed(bool ensureSuccess)
    {
        var content = new TrackedContent();
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        using var client = new BiliHttpClient(new Handler(response));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid");
        Assert.Equal("synthetic denied", await client.GetStringAsync(request, default, ensureSuccess));
        Assert.True(content.Disposed);
    }

    private sealed class TrackedContent : StringContent
    {
        public TrackedContent() : base("synthetic denied") { }
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class Handler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
