// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using RichasyKernel;

namespace Richasy.BiliKernel.Services.User.Core;

internal sealed class ViewLaterClient
{
    private readonly BiliHttpClient _httpClient;
    private readonly IAuthenticationService _authenticationService;
    private readonly BiliAuthenticator _authenticator;

    public ViewLaterClient(
        BiliHttpClient httpClient,
        IAuthenticationService authenticationService,
        BiliAuthenticator basicAuthenticator)
    {
        _httpClient = httpClient;
        _authenticationService = authenticationService;
        _authenticator = basicAuthenticator;
    }

    public async Task<(IReadOnlyList<VideoInformation>? Videos, int Count)> GetMyViewLaterAsync(int page = 0, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            { "pn", (page + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) },
            { "ps", "40" },
        };

        using var request = await _authenticator.CreateRestRequestAsync(HttpMethod.Get, new Uri(BiliApis.Account.ViewLaterList), parameters, cancellationToken).ConfigureAwait(false);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseObj = await BiliHttpClient.ParseAsync(response, SourceGenerationContext.Default.BiliDataResponseViewLaterSetResponse).ConfigureAwait(false);
        var videos = responseObj.Data?.List?.Select(p => p.ToVideoInformation()).ToList().AsReadOnly();
        return responseObj.Data is null
            ? throw new KernelException("无法获取稍后再看列表数据")
            : (videos, responseObj.Data.Count);
    }

    public async Task AddAsync(string aid, CancellationToken cancellationToken = default)
    {
        await _authenticationService.EnsureTokenAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new Dictionary<string, string>
        {
            { "aid", aid },
            { "toview_version", "v2" },
        };

        var request = BiliHttpClient.CreateRequest(HttpMethod.Post, new Uri(BiliApis.Account.ViewLaterAdd));
        _authenticator.AuthorizeRestRequest(request, parameters);
        await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string[] aids, CancellationToken cancellationToken = default)
    {
        await _authenticationService.EnsureTokenAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new Dictionary<string, string>
        {
            { "aid", string.Join(",", aids) },
        };

        var request = BiliHttpClient.CreateRequest(HttpMethod.Post, new Uri(BiliApis.Account.ViewLaterDelete));
        _authenticator.AuthorizeRestRequest(request, parameters);
        await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task CleanAsync(ViewLaterCleanType cleanType, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            { "clean_type",  ((int)cleanType).ToString() },
        };

        await _authenticationService.EnsureTokenAsync(cancellationToken).ConfigureAwait(false);
        var request = BiliHttpClient.CreateRequest(HttpMethod.Post, new Uri(BiliApis.Account.ViewLaterClear));
        _authenticator.AuthorizeRestRequest(request, parameters);
        await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

}
