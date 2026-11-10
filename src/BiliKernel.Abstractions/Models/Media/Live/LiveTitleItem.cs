// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Models.Media;

/// <summary>
/// 直播头衔：弹幕里只带 identification（如 title-233-1），图标由本表映射.
/// </summary>
/// <param name="Id">头衔标识，如 title-233-1.</param>
/// <param name="Name">头衔名称.</param>
/// <param name="Url">图标地址.</param>
/// <param name="Width">图标宽度.</param>
/// <param name="Height">图标高度.</param>
public sealed record LiveTitleItem(string Id, string Name, string Url, int Width, int Height);
