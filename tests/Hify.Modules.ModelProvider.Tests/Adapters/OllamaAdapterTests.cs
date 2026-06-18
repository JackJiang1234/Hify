using System.Net;

using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Adapters;

namespace Hify.Modules.ModelProvider.Tests.Adapters;

/// <summary>Ollama 适配器测试：chat、embedding、连通性探 /api/tags、NDJSON 流式。</summary>
public sealed class OllamaAdapterTests
{
    private static readonly ProviderConnection Connection = new()
    {
        ProviderType = ProviderTypes.Ollama,
        BaseUrl = "http://localhost:11434",
        AuthType = AuthTypes.None,
    };

    private static OllamaAdapter CreateAdapter(StubHandler handler) => new(new StubHttpClientFactory(handler));

    [Fact]
    public async Task ChatAsync_Success_ParsesMessageAndCounts()
    {
        var handler = new StubHandler(() => StubResponses.Json(HttpStatusCode.OK,
            """
            {"message":{"role":"assistant","content":"Hi there"},"done":true,"done_reason":"stop",
             "prompt_eval_count":8,"eval_count":3}
            """));
        var adapter = CreateAdapter(handler);
        var request = new ChatRequest { Messages = [new ChatMessage { Role = "user", Content = "Hi" }], MaxTokens = 64 };

        var result = await adapter.ChatAsync(Connection, "llama3", request, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("Hi there", result.Data!.Content);
        Assert.Equal("stop", result.Data.FinishReason);
        Assert.Equal(8, result.Data.PromptTokens);
        Assert.Equal(3, result.Data.CompletionTokens);
        Assert.Equal("/api/chat", handler.LastPath);
    }

    [Fact]
    public async Task EmbedAsync_Success_ParsesEmbeddings()
    {
        var handler = new StubHandler(() => StubResponses.Json(HttpStatusCode.OK,
            """{"embeddings":[[0.1,0.2],[0.3,0.4]],"prompt_eval_count":5}"""));
        var adapter = CreateAdapter(handler);
        var request = new EmbeddingRequest { Inputs = ["a", "b"] };

        var result = await adapter.EmbedAsync(Connection, "nomic-embed-text", request, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data!.Vectors.Count);
        Assert.Equal(0.3f, result.Data.Vectors[1][0]);
        Assert.Equal(5, result.Data.PromptTokens);
        Assert.Equal("/api/embed", handler.LastPath);
    }

    [Fact]
    public async Task TestConnectionAsync_Success_ProbesTags()
    {
        var handler = new StubHandler(() => StubResponses.Json(HttpStatusCode.OK, """{"models":[]}"""));
        var adapter = CreateAdapter(handler);

        var result = await adapter.TestConnectionAsync(Connection, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("/api/tags", handler.LastPath);
    }

    [Fact]
    public async Task ChatStreamAsync_Success_ParsesNdjsonDeltasThenFinal()
    {
        const string ndjson =
            "{\"message\":{\"content\":\"Hel\"},\"done\":false}\n" +
            "{\"message\":{\"content\":\"lo\"},\"done\":false}\n" +
            "{\"message\":{\"content\":\"\"},\"done\":true,\"done_reason\":\"stop\",\"prompt_eval_count\":6,\"eval_count\":2}\n";
        var handler = new StubHandler(() => StubResponses.Ndjson(ndjson));
        var adapter = CreateAdapter(handler);
        var request = new ChatRequest { Messages = [new ChatMessage { Role = "user", Content = "Hi" }], MaxTokens = 64 };

        var result = await adapter.ChatStreamAsync(Connection, "llama3", request, CancellationToken.None);
        Assert.Equal(200, result.Code);

        var chunks = new List<ChatStreamChunk>();
        await foreach (var chunk in result.Data!)
        {
            chunks.Add(chunk);
        }

        Assert.Equal("Hello", string.Concat(chunks.Where(c => !c.IsFinal).Select(c => c.Delta)));
        var final = chunks[^1];
        Assert.True(final.IsFinal);
        Assert.Equal("stop", final.FinishReason);
        Assert.Equal(6, final.PromptTokens);
        Assert.Equal(2, final.CompletionTokens);
    }
}
