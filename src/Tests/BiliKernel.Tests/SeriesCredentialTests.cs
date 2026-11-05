using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Authorization;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Services.Media;
using Richasy.BiliKernel.Services.User;
using Xunit;
using Richasy.BiliKernel.Bili;

namespace Richasy.BiliKernel.Tests;

/// <summary>Series mutations through public services and the real authenticator.</summary>
public sealed class SeriesCredentialTests
{
    [Theory]
    [InlineData("add", true, 0)]
    [InlineData("del", true, 0)]
    [InlineData("add", false, 0)]
    [InlineData("del", false, 0)]
    [InlineData("add", true, -101)]
    [InlineData("del", true, -403)]
    public async Task FollowUsesOnlyCookie(string operation, bool hasToken, int code)
        => await VerifyMutation(operation, hasToken, true, code).ConfigureAwait(true);

    [Theory]
    [InlineData(true, 0, PgcFavoriteStatus.Want)]
    [InlineData(true, 0, PgcFavoriteStatus.Watching)]
    [InlineData(false, 0, PgcFavoriteStatus.Watched)]
    [InlineData(true, -101, PgcFavoriteStatus.Want)]
    [InlineData(true, -403, PgcFavoriteStatus.Watching)]
    public async Task StatusUsesOnlyCookie(bool hasToken, int code, PgcFavoriteStatus status)
        => await VerifyMutation("status/update", hasToken, true, code, status).ConfigureAwait(true);

    [Theory]
    [InlineData("add")]
    [InlineData("del")]
    [InlineData("status/update")]
    public async Task MissingCookieIsRejectedByServer(string operation)
        => await VerifyMutation(operation, true, false, -101).ConfigureAwait(true);

    private static async Task VerifyMutation(string operation, bool hasToken, bool hasCookie, int code, PgcFavoriteStatus status = PgcFavoriteStatus.Watching)
    {
        var handler = new RecordingHttpMessageHandler().With("/pgc/web/follow/", $"{{\"code\":{code},\"message\":\"series response\"}}");
        var cookies = new FakeCookiesResolver();
        if (hasCookie) cookies.SaveCookies(new Dictionary<string, string> { ["SESSDATA"] = "series-session", ["bili_jct"] = "series-csrf" });
        var tokens = new FakeTokenResolver { Token = hasToken ? new BiliToken { AccessToken = "unrelated-app-token" } : null };
        var http = new BiliHttpClient(handler);
        var auth = new BiliAuthenticator(cookies, tokens, httpClient: http);
        var discovery = new EntertainmentDiscoveryService(http, auth);
        var favorites = new FavoriteService(http, new RejectTokenRefresh(), tokens, auth);
        Task Mutate() => operation switch
        {
            "add" => discovery.FollowAsync("2468"),
            "del" => discovery.UnfollowAsync("2468"),
            _ => favorites.MarkPgcAsync(new MediaIdentifier("2468", null, null), status),
        };
        if (code == 0) await Mutate().ConfigureAwait(true);
        else await Assert.ThrowsAsync<RichasyKernel.KernelException>(Mutate).ConfigureAwait(true);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/pgc/web/follow/" + operation, request.Path);
        Assert.Equal("POST", request.Method);
        Assert.Contains("season_id=2468", request.Body);
        Assert.DoesNotContain("access_key", request.Body);
        Assert.False(request.Query.ContainsKey("access_key"));
        Assert.DoesNotContain("w_rid", request.Body);
        var form = request.Body!.Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));
        var signedText = string.Join("&", form.Where(p => p.Key != "sign").Select(p => $"{p.Key}={p.Value}").OrderBy(p => p, StringComparer.Ordinal));
        var expectedSign = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(signedText + "54e6a9a31b911cd5fc0daa66ebf94bc4"))).ToLowerInvariant();
        Assert.Equal(expectedSign, form["sign"]);
        Assert.Equal("aa1e74ee4874176e", form["appkey"]);
        Assert.DoesNotContain("aid=", request.Body);
        Assert.DoesNotContain("media_id=", request.Body);
        if (hasCookie)
        {
            Assert.Contains("SESSDATA=series-session", request.Cookie);
            Assert.Contains("csrf=series-csrf", request.Body);
        }
        else
        {
            Assert.DoesNotContain("SESSDATA=", request.Cookie ?? string.Empty);
            Assert.Contains("csrf=", request.Body);
        }
        if (operation == "status/update") Assert.Contains($"status={(int)status}", request.Body);
        else Assert.DoesNotContain("status=", request.Body);
    }

    private sealed class RejectTokenRefresh : Richasy.BiliKernel.Bili.Authorization.IAuthenticationService
    {
        public Task EnsureTokenAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Cookie mutations must not refresh App tokens");
        public Task SignInAsync(AuthorizeExecutionSettings? settings = default, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SignOutAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
