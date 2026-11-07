// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Services.Media.Core.Live;
using RichasyKernel;

namespace Richasy.BiliKernel.Services.Media;

/// <summary>
/// 直播弹幕服务.
/// </summary>
public sealed class LiveDanmakuService : ILiveDanmakuService
{
    private static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultAuthTimeout = TimeSpan.FromSeconds(10);

    private readonly ILiveDanmakuTransport _transport;
    private readonly TimeProvider _timeProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly TimeSpan _heartbeatInterval;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveDanmakuService"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP 客户端.</param>
    /// <param name="authenticator">认证器.</param>
    /// <param name="tokenResolver">令牌解析器.</param>
    /// <param name="buvidManager">设备标识管理器.</param>
    public LiveDanmakuService(
        BiliHttpClient httpClient,
        BiliAuthenticator authenticator,
        IBiliTokenResolver tokenResolver,
        BuvidManager buvidManager)
        : this(new LiveDanmakuTransport(new LiveDanmakuApiClient(httpClient, authenticator, tokenResolver, buvidManager)))
    {
    }

    internal LiveDanmakuService(
        ILiveDanmakuTransport transport,
        TimeSpan? heartbeatInterval = null,
        TimeProvider? timeProvider = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _transport = transport;
        _heartbeatInterval = heartbeatInterval ?? DefaultHeartbeatInterval;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _delay = delay ?? Task.Delay;
    }

    /// <inheritdoc/>
    public async Task<ILiveDanmakuSession> ConnectAsync(string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new KernelException("直播间标识不能为空");
        }

        var endpoint = await _transport.ResolveEndpointAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (endpoint.Hosts.Count == 0)
        {
            throw new KernelException("无法获取直播弹幕服务器地址");
        }

        var client = new LiveDanmakuClient(
            _transport,
            _timeProvider,
            _delay,
            _heartbeatInterval,
            DefaultAuthTimeout,
            endpoint);
        return await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
    }
}
