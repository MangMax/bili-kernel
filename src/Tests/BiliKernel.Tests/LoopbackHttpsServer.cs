// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 回环地址上的测试用 HTTPS 服务器，用于观察 TLS 握手与请求完成情况。
/// </summary>
internal sealed class LoopbackHttpsServer : IDisposable
{
    /// <summary>
    /// 服务器返回的响应正文。
    /// </summary>
    public const string ResponseBody = "loopback-ok";

    private readonly TcpListener _listener;
    private readonly X509Certificate2 _certificate;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _acceptLoop;
    private readonly TimeSpan _handshakeDelay;
    private int _handshakeAttempts;
    private int _completedHandshakes;
    private int _completedRequests;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoopbackHttpsServer"/> class.
    /// </summary>
    /// <param name="handshakeDelay">TLS 握手前的延迟，用于测试取消。</param>
    public LoopbackHttpsServer(TimeSpan handshakeDelay = default)
    {
        _handshakeDelay = handshakeDelay;
        _certificate = CreateCertificate();
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var endpoint = (IPEndPoint)_listener.LocalEndpoint;
        BaseAddress = new Uri($"https://127.0.0.1:{endpoint.Port}/", UriKind.Absolute);
        _acceptLoop = AcceptLoopAsync();
    }

    /// <summary>
    /// 获取服务器的 HTTPS 根地址。
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// 获取服务器证书的指纹。
    /// </summary>
    public string CertificateThumbprint => _certificate.Thumbprint;

    /// <summary>
    /// 获取服务器收到的 TLS 握手尝试次数。
    /// </summary>
    public int HandshakeAttempts => Volatile.Read(ref _handshakeAttempts);

    /// <summary>
    /// 获取服务器完成的 TLS 握手次数。
    /// </summary>
    public int CompletedHandshakes => Volatile.Read(ref _completedHandshakes);

    /// <summary>
    /// 获取服务器完成的 HTTP 请求次数。
    /// </summary>
    public int CompletedRequests => Volatile.Read(ref _completedRequests);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        _listener.Dispose();
        try
        {
            _ = _acceptLoop.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // 关闭过程中被取消的握手不影响测试结果。
        }

        _cancellation.Dispose();
        _certificate.Dispose();
        GC.SuppressFinalize(this);
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddIpAddress(IPAddress.Loopback);
        alternativeNames.AddDnsName("localhost");
        request.CertificateExtensions.Add(alternativeNames.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, critical: true));
        var serverAuthentication = new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication");
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { serverAuthentication }, critical: false));
        using var selfSigned = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
        return X509CertificateLoader.LoadPkcs12(selfSigned.Export(X509ContentType.Pfx), password: null);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(_cancellation.Token).ConfigureAwait(false);
                await HandleClientAsync(client).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            Interlocked.Increment(ref _handshakeAttempts);
            try
            {
                if (_handshakeDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_handshakeDelay, _cancellation.Token).ConfigureAwait(false);
                }

                using var networkStream = client.GetStream();
                using var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);
                var options = new SslServerAuthenticationOptions
                {
                    ServerCertificate = _certificate,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = SslProtocols.None,
                };
                await sslStream.AuthenticateAsServerAsync(options, _cancellation.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _completedHandshakes);
                await RespondAsync(sslStream).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 客户端拒绝证书或取消连接时，服务器端握手会失败；测试只关心计数。
            }
        }
    }

    private async Task RespondAsync(SslStream sslStream)
    {
        var requestBuffer = new byte[1024];
        var request = new StringBuilder();
        while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await sslStream.ReadAsync(requestBuffer.AsMemory(), _cancellation.Token).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            request.Append(Encoding.ASCII.GetString(requestBuffer, 0, read));
        }

        var body = Encoding.UTF8.GetBytes(ResponseBody);
        var header = "HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n";
        await sslStream.WriteAsync(Encoding.ASCII.GetBytes(header).AsMemory(), _cancellation.Token).ConfigureAwait(false);
        await sslStream.WriteAsync(body.AsMemory(), _cancellation.Token).ConfigureAwait(false);
        await sslStream.FlushAsync(_cancellation.Token).ConfigureAwait(false);
        Interlocked.Increment(ref _completedRequests);
    }
}
