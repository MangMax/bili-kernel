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
    IList<VideoChapter>? chapters = null,
    string? playCheck = null,
    bool isPreview = false,
    IList<string>? previewUrls = null)
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
    public IList<VideoChapter>? Chapters { get; } = chapters;

    /// <summary>
    /// 服务端返回的播放校验标记.
    /// </summary>
    /// <remarks>
    /// 例如 PLAY_WHOLE 表示可完整播放，PLAY_PREVIEW 表示仅可试看；不支持该标记的播放路径为 null.
    /// </remarks>
    public string? PlayCheck { get; } = playCheck;

    /// <summary>
    /// 是否为仅可试看的播放结果.
    /// </summary>
    public bool IsPreview { get; } = isPreview;

    /// <summary>
    /// 仅可试看时可播放的地址列表.
    /// </summary>
    public IList<string>? PreviewUrls { get; } = previewUrls;
}
