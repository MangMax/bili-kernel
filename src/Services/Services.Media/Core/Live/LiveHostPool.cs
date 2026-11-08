// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 直播弹幕主机.
/// </summary>
/// <param name="HostName">主机名.</param>
/// <param name="WssPort">WSS 端口.</param>
internal sealed record LiveHost(string HostName, int WssPort)
{
    /// <summary>
    /// 构造 /sub WebSocket 地址.
    /// </summary>
    /// <returns>WebSocket 地址.</returns>
    public Uri ToUri()
    {
        // 弹幕服务器只在默认 443 上提供 wss；host_list 里的 wss_port（如 2245）是普通 ws 端口，
        // 拼进 wss 地址会被服务端直接关闭连接.
        return new Uri($"wss://{HostName}/sub");
    }
}

/// <summary>
/// 去重并轮换的直播主机列表.
/// </summary>
internal sealed class LiveHostPool
{
    private readonly List<LiveHost> _hosts = [];
    private int _index;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveHostPool"/> class.
    /// </summary>
    /// <param name="hosts">主机列表.</param>
    public LiveHostPool(IEnumerable<LiveHost> hosts) => Reset(hosts);

    /// <summary>
    /// 去重后的主机数量.
    /// </summary>
    public int Count => _hosts.Count;

    /// <summary>
    /// 取下一台主机并前进游标.
    /// </summary>
    /// <returns>主机.</returns>
    public LiveHost Next()
    {
        if (_hosts.Count == 0)
        {
            throw new InvalidOperationException("直播弹幕主机列表为空");
        }

        var host = _hosts[_index];
        _index = (_index + 1) % _hosts.Count;
        return host;
    }

    /// <summary>
    /// 用新的主机列表替换，并保留轮换游标.
    /// </summary>
    /// <param name="hosts">主机列表.</param>
    public void Reset(IEnumerable<LiveHost> hosts)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _hosts.Clear();
        foreach (var host in hosts)
        {
            if (string.IsNullOrWhiteSpace(host.HostName))
            {
                continue;
            }

            var key = host.HostName;
            if (seen.Add(key))
            {
                _hosts.Add(host);
            }
        }

        if (_hosts.Count > 0)
        {
            _index %= _hosts.Count;
        }
        else
        {
            _index = 0;
        }
    }
}