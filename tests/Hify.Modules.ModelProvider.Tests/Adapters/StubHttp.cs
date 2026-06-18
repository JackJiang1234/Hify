using System.Net;
using System.Text;

namespace Hify.Modules.ModelProvider.Tests.Adapters;

/// <summary>测试用 stub <see cref="HttpMessageHandler"/>：捕获请求路径/头/正文，返回预置响应（无网络）。</summary>
internal sealed class StubHandler(Func<HttpResponseMessage> responder) : HttpMessageHandler
{
    /// <summary>最近一次请求的绝对路径。</summary>
    public string? LastPath { get; private set; }

    /// <summary>最近一次请求正文。</summary>
    public string? LastBody { get; private set; }

    /// <summary>最近一次请求头快照（不区分大小写）。</summary>
    public IReadOnlyDictionary<string, string> LastHeaders { get; private set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastPath = request.RequestUri?.AbsolutePath;
        LastHeaders = request.Headers.ToDictionary(
            header => header.Key,
            header => string.Join(",", header.Value),
            StringComparer.OrdinalIgnoreCase);
        if (request.Content is not null)
        {
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return responder();
    }
}

/// <summary>把单一 handler 包装为 <see cref="IHttpClientFactory"/>，所有命名客户端共用。</summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

/// <summary>构造预置响应。</summary>
internal static class StubResponses
{
    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Sse(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };

    public static HttpResponseMessage Ndjson(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson") };
}
