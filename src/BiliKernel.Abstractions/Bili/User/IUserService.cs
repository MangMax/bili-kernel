// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models.Article;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.User;

namespace Richasy.BiliKernel.Bili.User;

/// <summary>
/// 用户服务.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// 获取用户信息.
    /// </summary>
    /// <returns><see cref="UserCard"/>.</returns>
    Task<UserCard> GetUserInformationAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取用户信息.
    /// </summary>
    /// <returns>用户列表.</returns>
    Task<IReadOnlyList<UserProfile>> BatchGetUsersAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户空间信息（WBI 的 Web 端点）.
    /// </summary>
    /// <param name="userId">用户 Id.</param>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns><see cref="UserCard"/>.</returns>
    Task<UserCard> GetUserSpaceInformationAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户投稿列表（WBI 的 Web 端点）.
    /// </summary>
    /// <param name="userId">用户 Id.</param>
    /// <param name="pageNumber">页码，从 0 开始.</param>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>视频列表与总数.</returns>
    Task<(IReadOnlyList<VideoInformation> Videos, int TotalCount)> GetUserVideosAsync(string userId, int pageNumber = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户专栏列表（WBI 的 Web 端点）.
    /// </summary>
    /// <param name="userId">用户 Id.</param>
    /// <param name="pageNumber">页码，从 0 开始.</param>
    /// <param name="cancellationToken">终止令牌.</param>
    /// <returns>专栏列表.</returns>
    Task<IReadOnlyList<ArticleInformation>> GetUserArticlesAsync(string userId, int pageNumber = 0, CancellationToken cancellationToken = default);
}
