// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Models.Media;

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 把接口返回的绝对播放地址映射为直播线路；游客旧接口返回的每条 durl 都是绝对地址.
/// </summary>
internal static class LivePlayUrlMapper
{
    /// <summary>
    /// 用游客旧接口的 durl 构造一条 FLV 线路.
    /// </summary>
    /// <param name="urls">绝对地址.</param>
    /// <param name="quality">清晰度标识.</param>
    /// <returns>直播线路；没有可用地址时返回 null.</returns>
    public static LiveLineInformation? CreateOldLine(IEnumerable<string> urls, int quality)
    {
        var list = new List<LivePlayUrl>();
        foreach (var url in urls)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                continue;
            }

            list.Add(new LivePlayUrl("http_stream", $"{uri.Scheme}://{uri.Authority}", uri.AbsolutePath, uri.Query));
        }

        return list.Count == 0
            ? null
            : new LiveLineInformation("avc", quality, [quality], list, "flv");
    }
}
