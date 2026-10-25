// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Models.Media;

/// <summary>
/// Dash 播放媒体信息.
/// </summary>
public sealed class DashMediaInformation(
    IList<DashSegmentInformation>? videoSegments,
    IList<DashSegmentInformation>? audioSegments,
    IList<PlayerFormatInformation>? formats,
    int duration = 0,
    IList<MediaChapter>? chapters = null)
{
    /// <summary>
    /// 视频时长.
    /// </summary>
    public int Duration { get; } = duration;

    /// <summary>
    /// 不同清晰度的视频列表.
    /// </summary>
    public IList<DashSegmentInformation>? Videos { get; } = videoSegments;

    /// <summary>
    /// 不同码率的音频列表.
    /// </summary>
    public IList<DashSegmentInformation>? Audios { get; } = audioSegments;

    /// <summary>
    /// 格式列表.
    /// </summary>
    public IList<PlayerFormatInformation>? Formats { get; } = formats;

    /// <summary>
    /// 视频章节列表.
    /// </summary>
    /// <remarks>
    /// 对应哔哩哔哩播放器中的“章节”功能，由 UP 主在视频中标记，可能为空.
    /// </remarks>
    public IList<MediaChapter>? Chapters { get; } = chapters;
}
