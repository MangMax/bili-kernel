using Bilibili.Metadata.Device;
using Google.Protobuf;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 客户端身份一致性测试。
/// </summary>
public sealed class ClientIdentityTests
{
    [Fact]
    public void RestBuildMatchesGrpcIdentity()
    {
        var cookies = new FakeCookiesResolver();
        using var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), new RecordingHttpMessageHandler());
        var authenticator = new BiliAuthenticator(cookies, new FakeTokenResolver(), buvid);

        var restRequest = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri("https://api.bilibili.com/x/web-interface/nav"));
        authenticator.AuthorizeRestRequest(restRequest, []);

        var query = QueryOf(restRequest.RequestUri!);
        Assert.Equal(BiliClientIdentity.BuildNumber, query["build"]);

        var grpcRequest = BiliHttpClient.CreateRequest(HttpMethod.Post, new Uri("https://grpc.biliapi.net/test"));
        authenticator.AuthorizeGrpcRequest(grpcRequest);

        var userAgent = string.Join(" ", grpcRequest.Headers.GetValues("user-agent"));
        Assert.Contains($"bili-universal/{BiliClientIdentity.BuildNumber} ", userAgent, StringComparison.Ordinal);

        var device = Device.Parser.ParseFrom(FromBase64(string.Join(",", grpcRequest.Headers.GetValues("x-bili-device-bin"))));
        Assert.Equal(BiliClientIdentity.Build, device.Build);
        Assert.Equal(BiliClientIdentity.DeviceModel, device.Model);
        Assert.Equal(BiliClientIdentity.OSVersion, device.Osver);
    }

    [Fact]
    public void RestAndGrpcSendTheSameBuvid()
    {
        var cookies = new FakeCookiesResolver();
        using var buvid = new BuvidManager(cookies, new InMemoryBuvidStore("B3", "B4"), new RecordingHttpMessageHandler());
        var authenticator = new BiliAuthenticator(cookies, new FakeTokenResolver(), buvid);

        var grpcRequest = BiliHttpClient.CreateRequest(HttpMethod.Post, new Uri("https://grpc.biliapi.net/test"));
        authenticator.AuthorizeGrpcRequest(grpcRequest);

        Assert.Equal("B3", string.Join(",", grpcRequest.Headers.GetValues("buvid")));
    }

    [Fact]
    public async Task ClientUsesWebUserAgentFromIdentity()
    {
        var handler = new RecordingHttpMessageHandler();
        using var client = new BiliHttpClient(handler);
        var request = BiliHttpClient.CreateRequest(HttpMethod.Get, new Uri("https://api.bilibili.com/x/web-interface/nav"));
        await client.SendAsync(request);

        Assert.Equal(BiliClientIdentity.WebUserAgent, handler.Requests.Single().Headers["User-Agent"]);
    }

    private static Dictionary<string, string> QueryOf(Uri uri)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = pair.IndexOf('=', StringComparison.Ordinal);
            result[Uri.UnescapeDataString(pair[..index])] = Uri.UnescapeDataString(pair[(index + 1)..]);
        }

        return result;
    }

    private static byte[] FromBase64(string text)
    {
        var normalized = text.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + ((4 - (normalized.Length % 4)) % 4), '=');
        return Convert.FromBase64String(normalized);
    }
}