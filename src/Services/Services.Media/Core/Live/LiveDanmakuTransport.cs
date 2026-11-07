// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Bili.Media;
using System.Net;
using System.Net.WebSockets;

namespace Richasy.BiliKernel.Services.Media.Core.Live;

/// <summary>
/// 生产传输实现：HTTP 端点解析 + ClientWebSocket.
/// </summary>
internal sealed class LiveDanmakuTransport : ILiveDanmakuTransport
{
    private readonly LiveDanmakuApiClient _apiClient;

    public LiveDanmakuTransport(LiveDanmakuApiClient apiClient) => _apiClient = apiClient;

    /// <inheritdoc/>
    public Task<LiveDanmakuEndpoint> ResolveEndpointAsync(string roomId, CancellationToken cancellationToken)
        => _apiClient.ResolveEndpointAsync(roomId, cancellationToken);

    /// <inheritdoc/>
    public async Task<ILiveDanmakuSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocketLiveDanmakuSocket();
        try
        {
            await socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch (Exception ex) when (ex is WebSocketException or HttpRequestException)
        {
            await socket.DisposeAsync().ConfigureAwait(false);
            var status = ex is HttpRequestException http
                ? http.StatusCode
                : (ex.InnerException as HttpRequestException)?.StatusCode;
            if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new LiveDanmakuUnauthorizedException((int)status.Value, ex.Message);
            }

            throw;
        }
    }
}
