using System.Net;
using System.Text;

using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Adapters;

namespace Hify.Modules.ModelProvider.Tests.Adapters;

/// <summary>
/// OpenAI 兼容适配器测试：用 stub <see cref="HttpMessageHandler"/> 打桩真实 HTTP 管道（无网络、无外部依赖），
/// 覆盖 chat / embedding / 连通性 / SSE 流式与失败映射。
/// </summary>
public sealed class OpenAiCompatibleAdapterTests
{
    private static readonly ProviderConnection Connection = new()
    {
        ProviderType = ProviderTypes.OpenAi,
        BaseUrl = "https://api.test/v1",
        AuthType = AuthTypes.Bearer,
        ApiKey = "sk-test-key",
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                // 在请求体被释放前抓取，供断言序列化字段。
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return responder(request);
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static OpenAiCompatibleAdapter CreateAdapter(StubHandler handler) =>
        new(new StubHttpClientFactory(handler));

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ChatAsync_Success_ParsesContentAndUsage()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """
            {"choices":[{"message":{"content":"Hi there"},"finish_reason":"stop"}],
             "usage":{"prompt_tokens":11,"completion_tokens":3}}
            """));
        var adapter = CreateAdapter(handler);
        var request = new ChatRequest { Messages = [new ChatMessage { Role = "user", Content = "Hi" }], MaxTokens = 64 };

        var result = await adapter.ChatAsync(Connection, "gpt-4o", request, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("Hi there", result.Data!.Content);
        Assert.Equal("stop", result.Data.FinishReason);
        Assert.Equal(11, result.Data.PromptTokens);
        Assert.Equal(3, result.Data.CompletionTokens);
    }

    [Fact]
    public async Task ChatAsync_AppliesBearerAuthAndHitsChatCompletions()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"choices":[{"message":{"content":"x"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1}}"""));
        var adapter = CreateAdapter(handler);
        var request = new ChatRequest { Messages = [new ChatMessage { Role = "user", Content = "Hi" }], MaxTokens = 16 };

        await adapter.ChatAsync(Connection, "gpt-4o", request, CancellationToken.None);

        Assert.Equal("/v1/chat/completions", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("sk-test-key", handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task ChatAsync_Payload_UsesMaxCompletionTokens()
    {
        // 回归：新版 OpenAI 模型拒绝 max_tokens，请求体须发 max_completion_tokens。
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"choices":[{"message":{"content":"x"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1}}"""));
        var adapter = CreateAdapter(handler);
        var request = new ChatRequest { Messages = [new ChatMessage { Role = "user", Content = "Hi" }], MaxTokens = 64 };

        await adapter.ChatAsync(Connection, "gpt-4o", request, CancellationToken.None);

        Assert.Contains("\"max_completion_tokens\":64", handler.LastBody);
        Assert.DoesNotContain("\"max_tokens\"", handler.LastBody);
    }

    [Fact]
    public async Task ChatAsync_Unauthorized_ReturnsAuthFailed()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.Unauthorized, """{"error":"bad key"}"""));
        var adapter = CreateAdapter(handler);
        var request = new ChatRequest { Messages = [new ChatMessage { Role = "user", Content = "Hi" }], MaxTokens = 16 };

        var result = await adapter.ChatAsync(Connection, "gpt-4o", request, CancellationToken.None);

        Assert.Equal(2002, result.Code); // ProviderAuthFailed
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task EmbedAsync_Success_ParsesVectors()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"data":[{"embedding":[0.1,0.2,0.3]},{"embedding":[0.4,0.5,0.6]}],"usage":{"prompt_tokens":7}}"""));
        var adapter = CreateAdapter(handler);
        var request = new EmbeddingRequest { Inputs = ["a", "b"] };

        var result = await adapter.EmbedAsync(Connection, "text-embedding-3-small", request, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data!.Vectors.Count);
        Assert.Equal(3, result.Data.Vectors[0].Count);
        Assert.Equal(0.4f, result.Data.Vectors[1][0]);
        Assert.Equal(7, result.Data.PromptTokens);
    }

    [Fact]
    public async Task TestConnectionAsync_Success_ReturnsLatency()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"data":[]}"""));
        var adapter = CreateAdapter(handler);

        var result = await adapter.TestConnectionAsync(Connection, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.True(result.Data!.LatencyMs >= 0);
        Assert.Equal("/v1/models", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task TestConnectionAsync_ServerError_Fails()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.InternalServerError, "boom"));
        var adapter = CreateAdapter(handler);

        var result = await adapter.TestConnectionAsync(Connection, CancellationToken.None);

        Assert.NotEqual(200, result.Code);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task ChatStreamAsync_Success_YieldsDeltasThenFinalWithUsage()
    {
        const string sse =
            "data: {\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}\n" +
            "\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\" world\"}}]}\n" +
            "\n" +
            "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n" +
            "\n" +
            "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":5,\"completion_tokens\":2}}\n" +
            "\n" +
            "data: [DONE]\n";
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sse, Encoding.UTF8, "text/event-stream"),
        });
        var adapter = CreateAdapter(handler);
        var request = new ChatRequest { Messages = [new ChatMessage { Role = "user", Content = "Hi" }], MaxTokens = 64 };

        var result = await adapter.ChatStreamAsync(Connection, "gpt-4o", request, CancellationToken.None);
        Assert.Equal(200, result.Code);

        var chunks = new List<ChatStreamChunk>();
        await foreach (var chunk in result.Data!)
        {
            chunks.Add(chunk);
        }

        var text = string.Concat(chunks.Where(c => !c.IsFinal).Select(c => c.Delta));
        Assert.Equal("Hello world", text);

        var final = chunks[^1];
        Assert.True(final.IsFinal);
        Assert.Equal("stop", final.FinishReason);
        Assert.Equal(5, final.PromptTokens);
        Assert.Equal(2, final.CompletionTokens);
    }

    [Fact]
    public async Task ChatStreamAsync_Unauthorized_ReturnsAuthFailed()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.Unauthorized, "nope"));
        var adapter = CreateAdapter(handler);
        var request = new ChatRequest { Messages = [new ChatMessage { Role = "user", Content = "Hi" }], MaxTokens = 16 };

        var result = await adapter.ChatStreamAsync(Connection, "gpt-4o", request, CancellationToken.None);

        Assert.Equal(2002, result.Code);
        Assert.Null(result.Data);
    }
}
