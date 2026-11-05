using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Services.Media;
using Xunit;

namespace Richasy.BiliKernel.Tests;

public sealed class PartitionCredentialTests
{
    [Theory]
    [InlineData(true, true, PartitionVideoSortType.Play, "view", 0, 0)]
    [InlineData(false, true, PartitionVideoSortType.Newest, "senddate", 1, 123)]
    [InlineData(true, false, PartitionVideoSortType.Danmaku, "danmaku", 2, 456)]
    [InlineData(false, false, PartitionVideoSortType.Favorite, "favorite", 0, 0)]
    [InlineData(true, true, PartitionVideoSortType.Reply, "reply", 3, 789)]
    public async Task SortedListsUseCookieWithoutAppToken(bool token, bool cookie, PartitionVideoSortType sort, string order, int page, long offset)
    {
        var (service, handler) = Create(token, cookie, """{"code":0,"data":{"archives":[{"param":"2468","title":"Partition result","duration":90}]}}""");
        var result = await service.GetChildPartitionVideoListAsync(new Partition("24", "Animation"), offset, page, sort);
        Assert.Equal("2468", Assert.Single(result.Videos).Identifier.Id);
        Assert.Equal(page + 1, result.NextPageNumber);
        Assert.Equal(0, result.Offset);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/x/web-interface/newlist", request.Path);
        Assert.Equal("24", request.Query["rid"]);
        Assert.Equal(order, request.Query["order"]);
        Assert.Equal((page + 1).ToString(), request.Query["pn"]);
        Assert.Equal("30", request.Query["ps"]);
        Assert.Equal("0", request.Query["pull"]);
        if (offset > 0) Assert.Equal(offset.ToString(), request.Query["ctime"]);
        else Assert.False(request.Query.ContainsKey("ctime"));
        Assert.False(request.Query.ContainsKey("access_key"));
        Assert.Null(request.Body);
        if (cookie) Assert.Contains("SESSDATA=partition-session", request.Cookie);
        else Assert.DoesNotContain("SESSDATA=", request.Cookie ?? "");
        Assert.False(request.Query.ContainsKey("w_rid"));
        Assert.False(request.Query.ContainsKey("csrf"));
        Assert.Equal("aa1e74ee4874176e", request.Query["appkey"]);
        var signedText = string.Join("&", request.Query.Where(p => p.Key != "sign").Select(p => $"{p.Key}={p.Value}").OrderBy(p => p, StringComparer.Ordinal));
        var expectedSign = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(signedText + "54e6a9a31b911cd5fc0daa66ebf94bc4"))).ToLowerInvariant();
        Assert.Equal(expectedSign, request.Query["sign"]);
    }

    [Theory]
    [InlineData(0, "无法获取到有效的视频列表，请稍后再试")]
    [InlineData(-101, "partition denied")]
    [InlineData(-403, "partition denied")]
    public async Task EmptyAndRejectedListsRemainErrors(int code, string message)
    {
        var (service, _) = Create(true, true, $"{{\"code\":{code},\"message\":\"partition denied\",\"data\":{{\"archives\":[]}}}}");
        var error = await Assert.ThrowsAsync<RichasyKernel.KernelException>(() => service.GetChildPartitionVideoListAsync(new Partition("24", "Animation"), sortType: PartitionVideoSortType.Play));
        Assert.Contains(message, error.Message);
    }

    [Theory]
    [InlineData(true, 0, "/x/v2/region/dynamic/child")]
    [InlineData(true, 123, "/x/v2/region/dynamic/child/list")]
    [InlineData(false, 0, "/x/v2/region/dynamic/child")]
    public async Task DefaultSortPreservesAppCredentialsAndCursor(bool token, long offset, string path)
    {
        var handler = new RecordingHttpMessageHandler().With(path, """{"code":0,"data":{"new":[{"param":"2468","title":"Default result"}],"cbottom":999}}""");
        var tokens = new FakeTokenResolver { Token = token ? new BiliToken { AccessToken = "intentional-app-token" } : null };
        var http = new BiliHttpClient(handler);
        var service = new VideoDiscoveryService(http, new BiliAuthenticator(new FakeCookiesResolver(), tokens, httpClient: http), tokens);
        var result = await service.GetChildPartitionVideoListAsync(new Partition("24", "Animation"), offset);
        Assert.Equal("2468", Assert.Single(result.Videos).Identifier.Id);
        Assert.Equal(999, result.Offset);
        Assert.Equal(1, result.NextPageNumber);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(path, request.Path);
        Assert.Equal("24", request.Query["rid"]);
        Assert.False(request.Query.ContainsKey("order"));
        Assert.False(request.Query.ContainsKey("pn"));
        if (offset > 0) Assert.Equal("123", request.Query["ctime"]);
        else Assert.False(request.Query.ContainsKey("ctime"));
        if (token) Assert.Equal("intentional-app-token", request.Query["access_key"]);
        else Assert.False(request.Query.ContainsKey("access_key"));
    }

    private static (VideoDiscoveryService Service, RecordingHttpMessageHandler Handler) Create(bool token, bool cookie, string json)
    {
        var handler = new RecordingHttpMessageHandler().With("/x/web-interface/newlist", json);
        var cookies = new FakeCookiesResolver();
        if (cookie) cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "partition-session" });
        var tokens = new FakeTokenResolver { Token = token ? new BiliToken { AccessToken = "unrelated-app-token" } : null };
        var http = new BiliHttpClient(handler);
        return (new VideoDiscoveryService(http, new BiliAuthenticator(cookies, tokens, httpClient: http), tokens), handler);
    }
}
