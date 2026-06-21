using System.Net;
using System.Text;
using System.Text.Json;

namespace Hify.IntegrationTests;

/// <summary>
/// Agent 管理的 HTTP 端到端测试（连不上测试 PG 则跳过）。
/// 先经 Provider/Model 接口建出一个 chat 模型，再以其 Id 建 Agent，验证方案 B 的引用校验与 Result/3xxx 形状。
/// </summary>
public sealed class AgentApiTests : IClassFixture<AgentApiTestFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private bool _available;

    public AgentApiTests(AgentApiTestFactory factory) => _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        try
        {
            var response = await _client.GetAsync("/api/v1/agents?page=1&size=1");
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

    // 建一个支持工具调用的 chat 模型，返回其 Id。
    private async Task<long> SeedChatModelAsync()
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
        return modelRoot.GetProperty("data").GetProperty("id").GetInt64();
    }

    private static object AgentBody(string name, long modelId) => new
    {
        name,
        description = "an agent",
        modelId,
        systemPrompt = "you are helpful",
        modelParams = new { temperature = 0.7, maxTokens = 1024 },
        retrievalParams = new { topK = 3, scoreThreshold = 0.5 },
        maxIterations = 5,
        toolIds = new[] { 10, 11 },
        knowledgeBaseIds = new[] { 20 },
        enabled = true,
    };

    [Fact]
    public async Task CreateAgent_WithValidModel_ReturnsBindings()
    {
        if (!_available)
        {
            return;
        }

        var modelId = await SeedChatModelAsync();

        var root = await ReadRootAsync(await _client.PostAsync("/api/v1/agents", JsonBody(AgentBody(UniqueName("agent"), modelId))));

        Assert.Equal(200, root.GetProperty("code").GetInt32());
        var data = root.GetProperty("data");
        Assert.True(data.GetProperty("id").GetInt64() > 0);
        Assert.Equal(2, data.GetProperty("toolIds").GetArrayLength());
        Assert.Equal(0.7, data.GetProperty("modelParams").GetProperty("temperature").GetDouble());
        Assert.Equal(3, data.GetProperty("retrievalParams").GetProperty("topK").GetInt32());
    }

    [Fact]
    public async Task CreateAgent_EmptyName_ReturnsParamInvalid()
    {
        if (!_available)
        {
            return;
        }

        var body = new { name = "", modelId = 1, systemPrompt = "x" };
        var root = await ReadRootAsync(await _client.PostAsync("/api/v1/agents", JsonBody(body)));

        Assert.Equal(1001, root.GetProperty("code").GetInt32()); // ParamInvalid（全局校验过滤器）
    }

    [Fact]
    public async Task CreateAgent_NonexistentModel_ReturnsModelInvalid()
    {
        if (!_available)
        {
            return;
        }

        var root = await ReadRootAsync(await _client.PostAsync("/api/v1/agents", JsonBody(AgentBody(UniqueName("agent"), 999_999_999))));

        Assert.Equal(3003, root.GetProperty("code").GetInt32()); // AgentModelInvalid
    }

    [Fact]
    public async Task GetAgent_AfterCreate_ReturnsIt()
    {
        if (!_available)
        {
            return;
        }

        var modelId = await SeedChatModelAsync();
        var created = await ReadRootAsync(await _client.PostAsync("/api/v1/agents", JsonBody(AgentBody(UniqueName("agent"), modelId))));
        var id = created.GetProperty("data").GetProperty("id").GetInt64();

        var root = await ReadRootAsync(await _client.GetAsync($"/api/v1/agents/{id}"));

        Assert.Equal(200, root.GetProperty("code").GetInt32());
        Assert.Equal(id, root.GetProperty("data").GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task UpdateAgent_ReplacesBindings()
    {
        if (!_available)
        {
            return;
        }

        var modelId = await SeedChatModelAsync();
        var created = await ReadRootAsync(await _client.PostAsync("/api/v1/agents", JsonBody(AgentBody(UniqueName("agent"), modelId))));
        var id = created.GetProperty("data").GetProperty("id").GetInt64();
        var name = created.GetProperty("data").GetProperty("name").GetString();

        var updateBody = new
        {
            name,
            modelId,
            systemPrompt = "updated",
            retrievalParams = new { topK = 5, scoreThreshold = 0.0 },
            maxIterations = 8,
            toolIds = new[] { 11, 12 },
            knowledgeBaseIds = Array.Empty<int>(),
            enabled = true,
        };
        var root = await ReadRootAsync(await _client.PutAsync($"/api/v1/agents/{id}", JsonBody(updateBody)));

        Assert.Equal(200, root.GetProperty("code").GetInt32());
        Assert.Equal(2, root.GetProperty("data").GetProperty("toolIds").GetArrayLength());
        Assert.Equal(0, root.GetProperty("data").GetProperty("knowledgeBaseIds").GetArrayLength());
        Assert.Equal(8, root.GetProperty("data").GetProperty("maxIterations").GetInt32());
    }

    [Fact]
    public async Task ListAgents_ReturnsPageResultShape()
    {
        if (!_available)
        {
            return;
        }

        var root = await ReadRootAsync(await _client.GetAsync("/api/v1/agents?page=1&size=20"));

        Assert.Equal(200, root.GetProperty("code").GetInt32());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("data").ValueKind);
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("size").GetInt32());
    }

    [Fact]
    public async Task DeleteAgent_ThenGet_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        var modelId = await SeedChatModelAsync();
        var created = await ReadRootAsync(await _client.PostAsync("/api/v1/agents", JsonBody(AgentBody(UniqueName("agent"), modelId))));
        var id = created.GetProperty("data").GetProperty("id").GetInt64();

        var deleted = await ReadRootAsync(await _client.DeleteAsync($"/api/v1/agents/{id}"));
        Assert.Equal(200, deleted.GetProperty("code").GetInt32());

        var fetched = await ReadRootAsync(await _client.GetAsync($"/api/v1/agents/{id}"));
        Assert.Equal(3001, fetched.GetProperty("code").GetInt32()); // AgentNotFound
    }

    [Fact]
    public async Task OpenApiDocument_IncludesAgents()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/agents", json, StringComparison.Ordinal);
    }
}
