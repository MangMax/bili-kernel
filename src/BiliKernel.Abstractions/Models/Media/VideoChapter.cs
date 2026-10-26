// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Models.Media;

/// <summary>
/// 视频章节信息.
/// </summary>
/// <remarks>
/// 哔哩哔哩允许 UP 主为视频标记章节，播放器中据此展示分段进度条.
/// 数据来自播放器接口 <c>view_points</c> 字段，与容器内嵌章节无关.
/// </remarks>
public sealed class VideoChapter(string title, int startTime, int endTime)
{
    /// <summary>
    /// 章节标题.
    /// </summary>
    public string Title { get; } = title;

    /// <summary>
    /// 章节起始时间（秒）.
    /// </summary>
    public int StartTime { get; } = startTime;

    /// <summary>
    /// 章节结束时间（秒）.
    /// </summary>
    public int EndTime { get; } = endTime;

    /// <summary>
    /// 章节时长（秒）.
    /// </summary>
    public int Duration => EndTime - StartTime;
}
