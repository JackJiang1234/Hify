using System.Net;
using System.Text;
using System.Text.Json;

namespace Hify.IntegrationTests;

/// <summary>
/// 对话引擎 HTTP 端到端测试（连不上测试 PG 则跳过）。LLM 由工厂替换为脚本化替身。
/// 流程：建 Provider→Model→Agent→Conversation，发消息消费 SSE 帧，再查历史验证落库。
/// </summary>
public sealed class ConversationApiTests : IClassFixture<ConversationApiTestFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private bool _available;

    public ConversationApiTests(ConversationApiTestFactory factory) => _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        try
        {
            var response = await _client.GetAsync("/api/v1/conversations?page=1&size=1");
            _available = response.StatusCode == HttpStatusCode.OK;
        }
        catch
        {
            _available = false;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static StringContent JsonBody(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadRootAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private async Task<long> SeedAgentAsync()
    {
        var providerBody = new
        {
            name = UniqueName("prov"),
            providerType = "openai",
            baseUrl = "https://api.test/v1",
            authType = "bearer",
            apiKey = "sk-secret-123456",
            settings = "{}",
            enabled = true,
        };
        var providerRoot = await ReadRootAsync(await _client.PostAsync("/api/v1/providers", JsonBody(providerBody)));
        var providerId = providerRoot.GetProperty("data").GetProperty("id").GetInt64();

        var modelBody = new
        {
            name = "gpt-4o",
            modelType = "chat",
            contextWindow = 128000,
            maxOutputTokens = 4096,
            supportsTools = true,
            enabled = true,
        };
        var modelRoot = await ReadRootAsync(await _client.PostAsync($"/api/v1/providers/{providerId}/models", JsonBody(modelBody)));
        var modelId = modelRoot.GetProperty("data").GetProperty("id").GetInt64();

        var agentBody = new
        {
            name = UniqueName("agent"),
            modelId,
            systemPrompt = "you are helpful",
            maxIterations = 5,
            retrievalParams = new { topK = 3, scoreThreshold = 0.0 },
            toolIds = Array.Empty<long>(),
            knowledgeBaseIds = Array.Empty<long>(),
            enabled = true,
        };
        var agentRoot = await ReadRootAsync(await _client.PostAsync("/api/v1/agents", JsonBody(agentBody)));
        return agentRoot.GetProperty("data").GetProperty("id").GetInt64();
    }

    private static List<JsonElement> ParseSseData(string body)
    {
        var events = new List<JsonElement>();
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("data: ", StringComparison.Ordinal))
            {
                events.Add(JsonDocument.Parse(trimmed["data: ".Length..]).RootElement.Clone());
            }
        }

        return events;
    }

    [Fact]
    public async Task SendMessage_StreamsSse_AndPersistsHistory()
    {
        if (!_available)
        {
            return;
        }

        var agentId = await SeedAgentAsync();

        // 建会话。
        var convRoot = await ReadRootAsync(await _client.PostAsync("/api/v1/conversations", JsonBody(new { agentId })));
        Assert.Equal(200, convRoot.GetProperty("code").GetInt32());
        var conversationId = convRoot.GetProperty("data").GetProperty("id").GetInt64();

        // 发消息：消费 SSE。
        var send = await _client.PostAsync($"/api/v1/conversations/{conversationId}/messages", JsonBody(new { content = "hi there" }));
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        Assert.Equal("text/event-stream", send.Content.Headers.ContentType!.MediaType);

        var events = ParseSseData(await send.Content.ReadAsStringAsync());
        var deltas = events.Where(e => e.GetProperty("type").GetString() == "delta")
            .Select(e => e.GetProperty("text").GetString()).ToList();
        Assert.Equal(new[] { "Hello", ", ", "world!" }, deltas);

        var done = Assert.Single(events, e => e.GetProperty("type").GetString() == "done");
        Assert.Equal("stop", done.GetProperty("finishReason").GetString());
        Assert.True(done.GetProperty("messageId").GetInt64() > 0);

        // 查历史：应有 user + assistant 两条（最新在前）。
        var historyRoot = await ReadRootAsync(await _client.GetAsync($"/api/v1/conversations/{conversationId}/messages?page=1&size=20"));
        Assert.Equal(200, historyRoot.GetProperty("code").GetInt32());
        Assert.Equal(2, historyRoot.GetProperty("total").GetInt64());
        var roles = historyRoot.GetProperty("data").EnumerateArray().Select(m => m.GetProperty("role").GetString()).ToList();
        Assert.Contains("user", roles);
        Assert.Contains("assistant", roles);
        var assistant = historyRoot.GetProperty("data").EnumerateArray().First(m => m.GetProperty("role").GetString() == "assistant");
        Assert.Equal("Hello, world!", assistant.GetProperty("content").GetString());

        // 标题回填为首条用户消息。
        var listRoot = await ReadRootAsync(await _client.GetAsync("/api/v1/conversations?page=1&size=50"));
        var created = listRoot.GetProperty("data").EnumerateArray().First(c => c.GetProperty("id").GetInt64() == conversationId);
        Assert.Equal("hi there", created.GetProperty("title").GetString());
    }

    [Fact]
    public async Task SendMessage_ConversationMissing_Returns4001()
    {
        if (!_available)
        {
            return;
        }

        var response = await _client.PostAsync("/api/v1/conversations/999999999/messages", JsonBody(new { content = "hi" }));
        var root = await ReadRootAsync(response);

        Assert.Equal(4001, root.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task SendMessage_EmptyContent_ReturnsValidationError()
    {
        if (!_available)
        {
            return;
        }

        var agentId = await SeedAgentAsync();
        var convRoot = await ReadRootAsync(await _client.PostAsync("/api/v1/conversations", JsonBody(new { agentId })));
        var conversationId = convRoot.GetProperty("data").GetProperty("id").GetInt64();

        var response = await _client.PostAsync($"/api/v1/conversations/{conversationId}/messages", JsonBody(new { content = "" }));
        var root = await ReadRootAsync(response);

        // 全局校验过滤器统一返回通用码 1001。
        Assert.Equal(1001, root.GetProperty("code").GetInt32());
    }
}
