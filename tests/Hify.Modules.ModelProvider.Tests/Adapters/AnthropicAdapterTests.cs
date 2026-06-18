using System.Net;

using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Adapters;

namespace Hify.Modules.ModelProvider.Tests.Adapters;

/// <summary>Claude 适配器测试：content 块抽取、system 抽到顶层、anthropic-version 头、嵌入不支持、SSE 流式。</summary>
public sealed class AnthropicAdapterTests
{
    private static readonly ProviderConnection Connection = new()
    {
        ProviderType = ProviderTypes.Claude,
        BaseUrl = "https://api.anthropic.test/v1",
        AuthType = AuthTypes.Header,
        AuthHeaderName = "x-api-key",
        ApiKey = "sk-ant-test",
    };

    private static AnthropicAdapter CreateAdapter(StubHandler handler) => new(new StubHttpClientFactory(handler));

    [Fact]
    public async Task ChatAsync_Success_ExtractsTextAndUsage()
    {
        var handler = new StubHandler(() => StubResponses.Json(HttpStatusCode.OK,
            """
            {"content":[{"type":"text","text":"Hello "},{"type":"text","text":"world"}],
             "stop_reason":"end_turn","usage":{"input_tokens":12,"output_tokens":4}}
            """));
        var adapter = CreateAdapter(handler);
        var request = new ChatRequest { Messages = [new ChatMessage { Role = "user", Content = "Hi" }], MaxTokens = 64 };

        var result = await adapter.ChatAsync(Connection, "claude-opus-4-8", request, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("Hello world", result.Data!.Content);
        Assert.Equal("end_turn", result.Data.FinishReason);
        Assert.Equal(12, result.Data.PromptTokens);
        Assert.Equal(4, result.Data.CompletionTokens);
        Assert.Equal("/v1/messages", handler.LastPath);
    }

    [Fact]
    public async Task ChatAsync_AppliesApiKeyHeaderAndAnthropicVersion()
    {
        var handler = new StubHandler(() => StubResponses.Json(HttpStatusCode.OK,
            """{"content":[{"type":"text","text":"x"}],"stop_reason":"end_turn","usage":{"input_tokens":1,"output_tokens":1}}"""));
        var adapter = CreateAdapter(handler);
        var request = new ChatRequest { Messages = [new ChatMessage { Role = "user", Content = "Hi" }], MaxTokens = 16 };

        await adapter.ChatAsync(Connection, "claude-opus-4-8", request, CancellationToken.None);

        Assert.Equal("sk-ant-test", handler.LastHeaders["x-api-key"]);
        Assert.True(handler.LastHeaders.ContainsKey("anthropic-version"));
    }

    [Fact]
    public async Task ChatAsync_LiftsSystemMessageToTopLevelField()
    {
        var handler = new StubHandler(() => StubResponses.Json(HttpStatusCode.OK,
            """{"content":[{"type":"text","text":"x"}],"stop_reason":"end_turn","usage":{"input_tokens":1,"output_tokens":1}}"""));
        var adapter = CreateAdapter(handler);
        var request = new ChatRequest
        {
            Messages =
            [
                new ChatMessage { Role = "system", Content = "You are terse." },
                new ChatMessage { Role = "user", Content = "Hi" },
            ],
            MaxTokens = 16,
        };

        await adapter.ChatAsync(Connection, "claude-opus-4-8", request, CancellationToken.None);

        Assert.Contains("\"system\":\"You are terse.\"", handler.LastBody, StringComparison.Ordinal);
        // system 不应混进 messages 数组
        Assert.DoesNotContain("\"role\":\"system\"", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmbedAsync_ReturnsNotSupported()
    {
        var handler = new StubHandler(() => StubResponses.Json(HttpStatusCode.OK, "{}"));
        var adapter = CreateAdapter(handler);

        var result = await adapter.EmbedAsync(Connection, "claude-opus-4-8", new EmbeddingRequest { Inputs = ["a"] }, CancellationToken.None);

        Assert.Equal(2006, result.Code); // EmbeddingNotSupported
    }

    [Fact]
    public async Task TestConnectionAsync_Success_ProbesModels()
    {
        var handler = new StubHandler(() => StubResponses.Json(HttpStatusCode.OK, """{"data":[]}"""));
        var adapter = CreateAdapter(handler);

        var result = await adapter.TestConnectionAsync(Connection, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("/v1/models", handler.LastPath);
        Assert.True(handler.LastHeaders.ContainsKey("anthropic-version"));
    }

    [Fact]
    public async Task ChatStreamAsync_Success_YieldsDeltasThenFinalWithUsage()
    {
        const string sse =
            "event: message_start\n" +
            "data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":9,\"output_tokens\":0}}}\n" +
            "\n" +
            "event: content_block_delta\n" +
            "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Hel\"}}\n" +
            "\n" +
            "event: content_block_delta\n" +
            "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"lo\"}}\n" +
            "\n" +
            "event: message_delta\n" +
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":2}}\n" +
            "\n" +
            "event: message_stop\n" +
            "data: {\"type\":\"message_stop\"}\n";
        var handler = new StubHandler(() => StubResponses.Sse(sse));
        var adapter = CreateAdapter(handler);
        var request = new ChatRequest { Messages = [new ChatMessage { Role = "user", Content = "Hi" }], MaxTokens = 64 };

        var result = await adapter.ChatStreamAsync(Connection, "claude-opus-4-8", request, CancellationToken.None);
        Assert.Equal(200, result.Code);

        var chunks = new List<ChatStreamChunk>();
        await foreach (var chunk in result.Data!)
        {
            chunks.Add(chunk);
        }

        Assert.Equal("Hello", string.Concat(chunks.Where(c => !c.IsFinal).Select(c => c.Delta)));
        var final = chunks[^1];
        Assert.True(final.IsFinal);
        Assert.Equal("end_turn", final.FinishReason);
        Assert.Equal(9, final.PromptTokens);
        Assert.Equal(2, final.CompletionTokens);
    }
}
