// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Models.Media;

/// <summary>
/// 直播间表情包里的一个表情.
/// </summary>
/// <param name="Text">发送文本，形如 [doge].</param>
/// <param name="Unique">表情唯一标识，形如 official_117.</param>
/// <param name="Url">图片地址.</param>
/// <param name="Width">原始宽度.</param>
/// <param name="Height">原始高度.</param>
public sealed record LiveEmoticonItem(string Text, string Unique, string Url, int Width, int Height);
