using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Hify.IntegrationTests;

/// <summary>
/// 知识库 / 文档管理的 HTTP 端到端测试（连不上测试 PG 则跳过）。
/// 嵌入经 <see cref="StubModelInvoker"/> 不触网，故覆盖 multipart 上传 → 分块嵌入入库 → 检索的完整链路。
/// </summary>
public sealed class KnowledgeApiTests : IClassFixture<KnowledgeApiTestFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private bool _available;

    public KnowledgeApiTests(KnowledgeApiTestFactory factory) => _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        try
        {
            var response = await _client.GetAsync("/api/v1/knowledge-bases?page=1&size=1");
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

    private static MultipartFormDataContent FileForm(string fileName, string content, string mediaType = "text/plain")
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        form.Add(fileContent, "file", fileName);
        return form;
    }

    // 建一个 1536 维 embedding 模型，返回其 Id。
    private async Task<long> SeedEmbeddingModelAsync()
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
            name = "text-embedding-3-small",
            modelType = "embedding",
            embeddingDimensions = 1536,
            enabled = true,
        };
        var modelRoot = await ReadRootAsync(await _client.PostAsync($"/api/v1/providers/{providerId}/models", JsonBody(modelBody)));
        return modelRoot.GetProperty("data").GetProperty("id").GetInt64();
    }

    private async Task<long> CreateKnowledgeBaseAsync(long embeddingModelId)
    {
        var body = new
        {
            name = UniqueName("kb"),
            description = "产品手册",
            embeddingModelId,
            chunkSize = 1000,
            chunkOverlap = 100,
        };
        var root = await ReadRootAsync(await _client.PostAsync("/api/v1/knowledge-bases", JsonBody(body)));
        return root.GetProperty("data").GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task CreateKnowledgeBase_Valid_Returns200()
    {
        if (!_available)
        {
            return;
        }

        var modelId = await SeedEmbeddingModelAsync();
        var body = new { name = UniqueName("kb"), embeddingModelId = modelId, chunkSize = 1000, chunkOverlap = 100 };

        var root = await ReadRootAsync(await _client.PostAsync("/api/v1/knowledge-bases", JsonBody(body)));

        Assert.Equal(200, root.GetProperty("code").GetInt32());
        Assert.True(root.GetProperty("data").GetProperty("id").GetInt64() > 0);
        Assert.Equal(1000, root.GetProperty("data").GetProperty("chunkSize").GetInt32());
    }

    [Fact]
    public async Task CreateKnowledgeBase_EmptyName_ReturnsParamInvalid()
    {
        if (!_available)
        {
            return;
        }

        var body = new { name = "", embeddingModelId = 1, chunkSize = 1000, chunkOverlap = 100 };

        var root = await ReadRootAsync(await _client.PostAsync("/api/v1/knowledge-bases", JsonBody(body)));

        Assert.Equal(1001, root.GetProperty("code").GetInt32()); // ParamInvalid（全局校验过滤器）
    }

    [Fact]
    public async Task UploadDocument_Multipart_CompletesWithChunks()
    {
        if (!_available)
        {
            return;
        }

        var modelId = await SeedEmbeddingModelAsync();
        var kbId = await CreateKnowledgeBaseAsync(modelId);

        using var form = FileForm("manual.txt", "退货政策为七天无理由退货。");
        var root = await ReadRootAsync(await _client.PostAsync($"/api/v1/knowledge-bases/{kbId}/documents", form));

        Assert.Equal(200, root.GetProperty("code").GetInt32());
        var data = root.GetProperty("data");
        Assert.Equal("txt", data.GetProperty("fileType").GetString());
        Assert.Equal("completed", data.GetProperty("status").GetString());
        Assert.True(data.GetProperty("chunkCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task UploadDocument_NonTxt_ReturnsUnsupportedFileType()
    {
        if (!_available)
        {
            return;
        }

        var modelId = await SeedEmbeddingModelAsync();
        var kbId = await CreateKnowledgeBaseAsync(modelId);

        using var form = FileForm("manual.pdf", "任意内容", mediaType: "application/pdf");
        var root = await ReadRootAsync(await _client.PostAsync($"/api/v1/knowledge-bases/{kbId}/documents", form));

        Assert.Equal(7007, root.GetProperty("code").GetInt32()); // UnsupportedFileType
    }

    [Fact]
    public async Task UploadDocument_NoFilePart_ReturnsParamInvalid()
    {
        if (!_available)
        {
            return;
        }

        var modelId = await SeedEmbeddingModelAsync();
        var kbId = await CreateKnowledgeBaseAsync(modelId);

        using var form = new MultipartFormDataContent { { new StringContent("x"), "other" } };
        var root = await ReadRootAsync(await _client.PostAsync($"/api/v1/knowledge-bases/{kbId}/documents", form));

        Assert.Equal(1001, root.GetProperty("code").GetInt32()); // 未提供文件
    }

    [Fact]
    public async Task ListDocuments_ReturnsPageResultShape()
    {
        if (!_available)
        {
            return;
        }

        var modelId = await SeedEmbeddingModelAsync();
        var kbId = await CreateKnowledgeBaseAsync(modelId);
        using var form = FileForm("a.txt", "一段内容");
        await _client.PostAsync($"/api/v1/knowledge-bases/{kbId}/documents", form);

        var root = await ReadRootAsync(await _client.GetAsync($"/api/v1/knowledge-bases/{kbId}/documents?page=1&size=20"));

        Assert.Equal(200, root.GetProperty("code").GetInt32());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("data").ValueKind);
        Assert.Equal(1, root.GetProperty("data").GetArrayLength());
        Assert.Equal(20, root.GetProperty("size").GetInt32());
    }

    [Fact]
    public async Task Search_AfterUpload_ReturnsHit()
    {
        if (!_available)
        {
            return;
        }

        var modelId = await SeedEmbeddingModelAsync();
        var kbId = await CreateKnowledgeBaseAsync(modelId);
        const string content = "退货政策为七天无理由退货。";
        using var form = FileForm("refund.txt", content);
        await _client.PostAsync($"/api/v1/knowledge-bases/{kbId}/documents", form);

        var searchBody = new { query = content, topK = 3, scoreThreshold = 0.0 };
        var root = await ReadRootAsync(await _client.PostAsync($"/api/v1/knowledge-bases/{kbId}/search", JsonBody(searchBody)));

        Assert.Equal(200, root.GetProperty("code").GetInt32());
        var data = root.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.True(data.GetArrayLength() >= 1);
        // 与查询文本相同的分块应被命中并带来源文档名。
        Assert.Equal(content, data[0].GetProperty("content").GetString());
        Assert.Equal("refund.txt", data[0].GetProperty("documentName").GetString());
    }

    [Fact]
    public async Task DeleteKnowledgeBase_ThenGet_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        var modelId = await SeedEmbeddingModelAsync();
        var kbId = await CreateKnowledgeBaseAsync(modelId);

        var deleted = await ReadRootAsync(await _client.DeleteAsync($"/api/v1/knowledge-bases/{kbId}"));
        Assert.Equal(200, deleted.GetProperty("code").GetInt32());

        var fetched = await ReadRootAsync(await _client.GetAsync($"/api/v1/knowledge-bases/{kbId}"));
        Assert.Equal(7001, fetched.GetProperty("code").GetInt32()); // KnowledgeBaseNotFound
    }

    [Fact]
    public async Task OpenApiDocument_IncludesKnowledgeBases()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/knowledge-bases", json, StringComparison.Ordinal);
    }
}
