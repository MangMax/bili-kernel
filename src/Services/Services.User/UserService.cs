// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.User;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Services.User.Core;

namespace Richasy.BiliKernel.Services.User;

/// <summary>
/// 用户服务.
/// </summary>
public sealed class UserService : IUserService
{
    private readonly MyClient _myClient;
    private readonly SpaceClient _spaceClient;

    /// <summary>
    /// 初始化 <see cref="UserService"/> 类的新实例.
    /// </summary>
    public UserService(
        BiliHttpClient biliHttpClient,
        IAuthenticationService authenticationService,
        IBiliTokenResolver tokenResolver,
        BiliAuthenticator basicAuthenticator)
    {
        _myClient = new MyClient(biliHttpClient, authenticationService, tokenResolver, basicAuthenticator);
        _spaceClient = new SpaceClient(biliHttpClient, basicAuthenticator);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<UserProfile>> BatchGetUsersAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
        => _myClient.BatchGetUsersAsync(userIds, cancellationToken);

    /// <inheritdoc/>
    public Task<UserCard> GetUserInformationAsync(string userId, CancellationToken cancellationToken = default)
        => _myClient.GetUserCardAsync(userId, cancellationToken);

    /// <inheritdoc/>
    public Task<UserCard> GetUserSpaceInformationAsync(string userId, CancellationToken cancellationToken = default)
        => _spaceClient.GetUserSpaceInformationAsync(userId, cancellationToken);

    /// <inheritdoc/>
    public Task<(IReadOnlyList<VideoInformation> Videos, int TotalCount)> GetUserVideosAsync(string userId, int pageNumber = 0, CancellationToken cancellationToken = default)
        => _spaceClient.GetUserVideosAsync(userId, pageNumber, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ArticleInformation>> GetUserArticlesAsync(string userId, int pageNumber = 0, CancellationToken cancellationToken = default)
        => _spaceClient.GetUserArticlesAsync(userId, pageNumber, cancellationToken);
}