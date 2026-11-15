// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models.Appearance;

namespace Richasy.BiliKernel.Models.User;

/// <summary>
/// 用户合集或系列.
/// </summary>
public sealed class UserCollectionInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserCollectionInfo"/> class.
    /// </summary>
    /// <param name="id">合集 Id.</param>
    /// <param name="title">标题.</param>
    /// <param name="cover">封面.</param>
    /// <param name="count">内容数量.</param>
    /// <param name="description">简介.</param>
    /// <param name="isSeason">是否为合集（否则为系列）.</param>
    public UserCollectionInfo(
        string id,
        string? title,
        BiliImage? cover = default,
        int? count = default,
        string? description = default,
        bool isSeason = false)
    {
        Id = id;
        Title = title;
        Cover = cover;
        Count = count;
        Description = description;
        IsSeason = isSeason;
    }

    /// <summary>
    /// 合集 Id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// 封面.
    /// </summary>
    public BiliImage? Cover { get; }

    /// <summary>
    /// 内容数量.
    /// </summary>
    public int? Count { get; }

    /// <summary>
    /// 简介.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// 是否为合集；否则为系列.
    /// </summary>
    public bool IsSeason { get; }
}
