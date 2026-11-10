// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Models.Media;

/// <summary>
/// 直播地址拼接信息.
/// </summary>
public sealed class LivePlayUrl(string protocol, string host, string route, string query)
{
    /// <summary>
    /// 域名.
    /// </summary>
    public string Host { get; } = host;

    /// <summary>
    /// 路由.
    /// </summary>
    public string Route { get; } = route;

    /// <summary>
    /// 查询参数.
    /// </summary>
    public string Query { get; } = query;

    /// <summary>
    /// 协议.
    /// </summary>
    public string Protocol { get; set; } = protocol;

    /// <inheritdoc/>
    public override string ToString()
    {
        // HLS 的 base_url 自带结尾 '?'，部分节点返回的 extra 又以 '?' 开头；
        // 直接拼接会生成 "...m3u8??expires=..."，腾讯系 CDN 会按错误签名返回 403.
        var route = Route ?? string.Empty;
        var query = Query ?? string.Empty;
        if (query.StartsWith('?'))
        {
            query = query[1..];
        }

        if (query.Length == 0)
        {
            return $"{Host}{route}";
        }

        return route.EndsWith('?')
            ? $"{Host}{route}{query}"
            : $"{Host}{route}?{query}";
    }
}
