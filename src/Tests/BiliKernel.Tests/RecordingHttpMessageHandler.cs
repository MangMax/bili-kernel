using System.Net;
using System.Text;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 被记录下来的出站请求。
/// </summary>
internal sealed class RecordedRequest
{
    public string Method { get; init; } = "GET";

    public Uri Uri { get; init; } = new("https://api.bilibili.com");

    public string Path => Uri.AbsolutePath;

    public IReadOnlyDictionary<string, string> Query { get; init; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    public string? Body { get; init; }

    public string? Cookie => Headers.TryGetValue("Cookie", out var value) ? value : null;
}

/// <summary>
/// 记录出站请求并按配置返回响应的消息处理器。
/// </summary>
internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<RecordedRequest, HttpResponseMessage> _responder;
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new(StringComparer.OrdinalIgnoreCase);

    public RecordingHttpMessageHandler()
    {
        _responder = _ => Json(@"{""code"":0}");
    }

    public RecordingHttpMessageHandler(Func<RecordedRequest, HttpResponseMessage> responder) => _responder = responder;

    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>
    /// 为某个路径片段注册固定响应。
    /// </summary>
    public RecordingHttpMessageHandler With(string pathFragment, string json)
    {
        _routes[pathFragment] = () => Json(json);
        return this;
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var recorded = await RecordAsync(request).ConfigureAwait(false);
        Requests.Add(recorded);
        foreach (var route in _routes)
        {
            if (recorded.Path.Contains(route.Key, StringComparison.OrdinalIgnoreCase))
            {
                return route.Value();
            }
        }

        return _responder(recorded);
    }

    private static async Task<RecordedRequest> RecordAsync(HttpRequestMessage request)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(" ", header.Value);
        }

        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
            {
                headers[header.Key] = string.Join(" ", header.Value);
            }
        }

        var body = request.Content == null ? null : await request.Content.ReadAsStringAsync().ConfigureAwait(false);
        return new RecordedRequest
        {
            Method = request.Method.Method,
            Uri = request.RequestUri!,
            Query = ParseQuery(request.RequestUri!),
            Headers = headers,
            Body = body,
        };
    }

    private static Dictionary<string, string> ParseQuery(Uri uri)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        var text = uri.Query.TrimStart('?');
        if (string.IsNullOrEmpty(text))
        {
            return query;
        }

        foreach (var pair in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = pair.IndexOf('=', StringComparison.Ordinal);
            var key = index < 0 ? pair : pair[..index];
            var value = index < 0 ? string.Empty : pair[(index + 1)..];
            query[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
        }

        return query;
    }
}