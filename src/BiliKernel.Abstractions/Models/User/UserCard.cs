// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models.Appearance;

namespace Richasy.BiliKernel.Models.User;

/// <summary>
/// 用户资料卡，包含基本信息和社区数据.
/// </summary>
public sealed class UserCard
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserCard"/> class.
    /// </summary>
    /// 
    public UserCard(
        UserDetailProfile? profile,
        UserCommunityInformation? community)
    {
        Profile = profile;
        Community = community;
    }

    /// <summary>
    /// 资料.
    /// </summary>
    public UserDetailProfile? Profile { get; set; }

    /// <summary>
    /// 社区数据.
    /// </summary>
    public UserCommunityInformation? Community { get; set; }

    /// <summary>
    /// 用户空间背景图.
    /// </summary>
    public BiliImage? Banner { get; set; }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is UserCard card && EqualityComparer<UserDetailProfile>.Default.Equals(Profile, card.Profile);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Profile);
}
