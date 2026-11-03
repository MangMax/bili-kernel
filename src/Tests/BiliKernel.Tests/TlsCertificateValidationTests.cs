// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Http;
using RichasyKernel;
using System.Security.Authentication;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 默认 HTTP 处理器的 TLS 证书校验行为测试。
/// </summary>
public sealed class TlsCertificateValidationTests
{
    private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task DefaultClientRejectsUntrustedLoopbackCertificate()
    {
        using var server = new LoopbackHttpsServer();
        using var client = new BiliHttpClient();
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, server.BaseAddress);

        var exception = await Assert.ThrowsAsync<KernelException>(() => client.SendAsync(request));

        Assert.Contains(Flatten(exception), item => item is AuthenticationException);
        await WaitUntilAsync(() => server.HandshakeAttempts > 0, ObservationTimeout);
        Assert.Equal(0, server.CompletedRequests);
    }

    [Fact]
    public async Task ExplicitTrustSeamAcceptsLoopbackCertificate()
    {
        using var server = new LoopbackHttpsServer();
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                certificate is not null
                && string.Equals(certificate.Thumbprint, server.CertificateThumbprint, StringComparison.OrdinalIgnoreCase),
        };
        using var client = new BiliHttpClient(handler);
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, server.BaseAddress);

        var body = await client.GetStringAsync(request);

        Assert.Equal(LoopbackHttpsServer.ResponseBody, body);
        await WaitUntilAsync(() => server.CompletedRequests == 1, ObservationTimeout);
    }

    [Fact]
    public async Task AlreadyCanceledTokenIsNotWrapped()
    {
        using var server = new LoopbackHttpsServer();
        using var client = new BiliHttpClient();
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, server.BaseAddress);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetStringAsync(request, cancellation.Token));

        Assert.IsNotType<KernelException>(exception);
        Assert.Equal(0, server.CompletedRequests);
    }

    [Fact]
    public async Task CancellationDuringHandshakeIsNotWrapped()
    {
        using var server = new LoopbackHttpsServer(TimeSpan.FromSeconds(20));
        using var client = new BiliHttpClient();
        using var request = BiliHttpClient.CreateRequest(HttpMethod.Get, server.BaseAddress);
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(300));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetStringAsync(request, cancellation.Token));

        Assert.IsNotType<KernelException>(exception);
        await WaitUntilAsync(() => server.HandshakeAttempts > 0, ObservationTimeout);
        Assert.Equal(0, server.CompletedRequests);
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        var pending = new Queue<Exception>();
        pending.Enqueue(exception);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    pending.Enqueue(inner);
                }
            }
            else if (current.InnerException is not null)
            {
                pending.Enqueue(current.InnerException);
            }

            yield return current;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                var message = "服务器没有在预期时间内观察到 TLS 握手或请求。";
                throw new TimeoutException(message);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
        }
    }
}
