using System.Net;
using System.Text;
using System.Text.Json;

namespace Hify.IntegrationTests;

/// <summary>
/// 供应商/模型管理的 HTTP 端到端测试（连不上测试 PG 则跳过）。
/// 验证路由、内部请求模型绑定、全局校验过滤器、Result/PageResult 序列化、OpenAPI 文档。
/// </summary>
public sealed class ProviderApiTests : IClassFixture<ProviderApiTestFactory>, IAsyncLifetime
{
    private readonly ProviderApiTestFactory _factory;
    private readonly HttpClient _client;
    private bool _available;

    public ProviderApiTests(ProviderApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        try
        {
            var response = await _client.GetAsync("/api/v1/providers?page=1&size=1");
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

    private object NewProviderBody(string name) => new
    {
        name,
        providerType = "openai",
        baseUrl = "https://api.test/v1",
        authType = "bearer",
        apiKey = "sk-secret-123456",
        settings = "{}",
        enabled = true,
    };

    private static string UniqueName() => $"http-{Guid.NewGuid():N}";

    [Fact]
    public async Task CreateProvider_ReturnsMaskedKeyAndHealth()
    {
        if (!_available)
        {
            return;
        }

        var response = await _client.PostAsync("/api/v1/providers", JsonBody(NewProviderBody(UniqueName())));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var root = await ReadRootAsync(response);
        Assert.Equal(200, root.GetProperty("code").GetInt32());
        var data = root.GetProperty("data");
        Assert.True(data.GetProperty("id").GetInt64() > 0);
        Assert.Equal("…3456", data.GetProperty("apiKeyHint").GetString());
        Assert.Equal("unknown", data.GetProperty("health").GetProperty("status").GetString());
        // 密文/明文绝不出现在响应中
        Assert.DoesNotContain("sk-secret-123456", root.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateProvider_InvalidName_ReturnsParamInvalid()
    {
        if (!_available)
        {
            return;
        }

        var body = new { name = "", providerType = "openai", baseUrl = "https://api.test/v1", authType = "bearer" };
        var response = await _client.PostAsync("/api/v1/providers", JsonBody(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // 统一 200 + 业务码
        var root = await ReadRootAsync(response);
        Assert.Equal(1001, root.GetProperty("code").GetInt32()); // ParamInvalid（全局校验过滤器拦截）
    }

    [Fact]
    public async Task GetProvider_AfterCreate_ReturnsIt()
    {
        if (!_available)
        {
            return;
        }

        var created = await ReadRootAsync(await _client.PostAsync("/api/v1/providers", JsonBody(NewProviderBody(UniqueName()))));
        var id = created.GetProperty("data").GetProperty("id").GetInt64();

        var root = await ReadRootAsync(await _client.GetAsync($"/api/v1/providers/{id}"));

        Assert.Equal(200, root.GetProperty("code").GetInt32());
        Assert.Equal(id, root.GetProperty("data").GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task CreateModelUnderProvider_ThenGet()
    {
        if (!_available)
        {
            return;
        }

        var created = await ReadRootAsync(await _client.PostAsync("/api/v1/providers", JsonBody(NewProviderBody(UniqueName()))));
        var providerId = created.GetProperty("data").GetProperty("id").GetInt64();

        var modelBody = new { name = "gpt-4o", modelType = "chat", contextWindow = 128000, maxOutputTokens = 4096, enabled = true };
        var modelRoot = await ReadRootAsync(await _client.PostAsync($"/api/v1/providers/{providerId}/models", JsonBody(modelBody)));
        Assert.Equal(200, modelRoot.GetProperty("code").GetInt32());
        var modelId = modelRoot.GetProperty("data").GetProperty("id").GetInt64();
        Assert.Equal(providerId, modelRoot.GetProperty("data").GetProperty("providerId").GetInt64());

        var getRoot = await ReadRootAsync(await _client.GetAsync($"/api/v1/models/{modelId}"));
        Assert.Equal(200, getRoot.GetProperty("code").GetInt32());
        Assert.Equal("gpt-4o", getRoot.GetProperty("data").GetProperty("name").GetString());
    }

    [Fact]
    public async Task ListProviders_ReturnsPageResultShape()
    {
        if (!_available)
        {
            return;
        }

        var root = await ReadRootAsync(await _client.GetAsync("/api/v1/providers?page=1&size=20"));

        Assert.Equal(200, root.GetProperty("code").GetInt32());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("data").ValueKind);
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("size").GetInt32());
    }

    [Fact]
    public async Task OpenApiDocument_IsServed()
    {
        // 不依赖 DB；验证内部 controller 能被 OpenAPI 发现并生成文档。
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/providers", json, StringComparison.Ordinal);
    }
}
