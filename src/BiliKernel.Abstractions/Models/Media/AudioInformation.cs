// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Models.Media;

/// <summary>
/// 音频投稿信息.
/// </summary>
public sealed class AudioInformation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AudioInformation"/> class.
    /// </summary>
    /// <param name="identifier">音频标识.</param>
    /// <param name="author">音频作者.</param>
    /// <param name="duration">时长，以秒为单位.</param>
    /// <param name="playCount">播放数.</param>
    public AudioInformation(
        MediaIdentifier identifier,
        string? author = default,
        long? duration = default,
        long? playCount = default)
    {
        Identifier = identifier;
        Author = author;
        Duration = duration;
        PlayCount = playCount;
    }

    /// <summary>
    /// 音频标识.
    /// </summary>
    public MediaIdentifier Identifier { get; }

    /// <summary>
    /// 音频作者.
    /// </summary>
    public string? Author { get; }

    /// <summary>
    /// 音频时长，以秒为单位.
    /// </summary>
    public long? Duration { get; }

    /// <summary>
    /// 播放数.
    /// </summary>
    public long? PlayCount { get; }
}
