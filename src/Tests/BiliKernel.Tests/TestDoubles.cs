// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Models.Authorization;

namespace Richasy.BiliKernel.Tests;

internal sealed class FakeCookiesResolver : IBiliCookiesResolver
{
    private Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Cookies => _cookies;

    public string GetCookieString() => string.Join("; ", _cookies.Select(p => $"{p.Key}={p.Value}"));

    public IDictionary<string, string> GetCookies() => new Dictionary<string, string>(_cookies, StringComparer.Ordinal);

    public void SaveCookies(IDictionary<string, string> cookies) => _cookies = new Dictionary<string, string>(cookies, StringComparer.Ordinal);

    public void RemoveCookies() => _cookies.Clear();
}

internal sealed class FakeTokenResolver : IBiliTokenResolver
{
    public BiliToken? Token { get; set; }

    public BiliToken? GetToken() => Token;

    public void SaveToken(BiliToken token) => Token = token;

    public void RemoveToken() => Token = null;
}

internal sealed class InMemoryBuvidStore : IBuvidStore
{
    public InMemoryBuvidStore(string? buvid3 = null, string? buvid4 = null)
    {
        Buvid3 = buvid3;
        Buvid4 = buvid4;
    }

    public string? Buvid3 { get; private set; }

    public string? Buvid4 { get; private set; }

    public void Save(string buvid3, string? buvid4)
    {
        Buvid3 = buvid3;
        Buvid4 = buvid4;
    }
}

internal sealed class FakeAuthenticationService : IAuthenticationService
{
    public Task SignInAsync(AuthorizeExecutionSettings? settings = default, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task EnsureTokenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}