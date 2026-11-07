// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Models.Media;

/// <summary>
/// 直播线路信息.
/// </summary>
/// <param name="name">解码名.</param>
/// <param name="quality">清晰度标识.</param>
/// <param name="acceptQualities">支持的清晰度标识.</param>
/// <param name="urls">播放地址列表.</param>
/// <param name="formatName">接口返回的容器格式名（如 <c>ts</c>/<c>fmp4</c>）；未知时为空.</param>
public sealed class LiveLineInformation(
    string name,
    int quality,
    IList<int> acceptQualities,
    IList<LivePlayUrl> urls,
    string? formatName = null)
{
    /// <summary>
    /// 解码名.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets 接口返回的容器格式名（如 <c>ts</c>/<c>fmp4</c>）；接口没有给出时为空。
    /// </summary>
    /// <remarks>
    /// 用于播放地址的容器排序：只从地址推导会把无标记的 m3u8 当成 ts。
    /// 这是可选信息，为空时调用方应回退到地址推断。
    /// </remarks>
    public string? FormatName { get; } = formatName;

    /// <summary>
    /// 清晰度标识.
    /// </summary>
    public int Quality { get; } = quality;

    /// <summary>
    /// 支持的清晰度标识.
    /// </summary>
    public IList<int> AcceptQualities { get; } = acceptQualities;

    /// <summary>
    /// 播放地址列表.
    /// </summary>
    public IList<LivePlayUrl> Urls { get; } = urls;
}
