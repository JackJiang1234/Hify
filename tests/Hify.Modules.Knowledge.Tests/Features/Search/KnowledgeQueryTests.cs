using Hify.Contracts.Knowledge;
using Hify.Modules.Knowledge.Features.Documents;
using Hify.Modules.Knowledge.Features.KnowledgeBases;
using Hify.Modules.Knowledge.Features.Search;
using Hify.Modules.Knowledge.Persistence;
using Hify.Modules.Knowledge.Tests.Support;

namespace Hify.Modules.Knowledge.Tests.Features.Search;

/// <summary>
/// 检索（IKnowledgeQuery.SearchAsync）的真实库测试（连不上则跳过）。用内容确定性的嵌入替身：
/// 相同文本得相同向量，故"查询等于某分块文本"时该分块余弦距离 0、相似度最高、排最前。
/// 共用同一替身实例做入库与查询，保证向量空间一致。
/// </summary>
public sealed class KnowledgeQueryTests : IAsyncLifetime
{
    private const long EmbeddingModelId = 1;
    private bool _available;
    private readonly FakeModelInvoker _invoker = new(dimensions: 1536);

    public async Task InitializeAsync() => _available = await KnowledgeTestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static FakeModelProviderQuery FakeWith1536Model() =>
        new FakeModelProviderQuery().Add(FakeModelProviderQuery.EmbeddingModel(EmbeddingModelId, dimensions: 1536));

    private async Task<long> NewKbAsync(KnowledgeDbContext db)
    {
        var created = await new KnowledgeBaseService(db, FakeWith1536Model()).CreateAsync(
            new CreateKnowledgeBaseRequest { Name = $"it-{Guid.NewGuid():N}", EmbeddingModelId = EmbeddingModelId, ChunkSize = 1000, ChunkOverlap = 100 },
            CancellationToken.None);
        return created.Data!.Id;
    }

    private async Task UploadAsync(KnowledgeDbContext db, long kbId, string fileName, string content) =>
        await new DocumentService(db, _invoker).UploadAsync(
            new UploadDocumentRequest { KnowledgeBaseId = kbId, FileName = fileName, Content = content },
            CancellationToken.None);

    [Fact]
    public async Task SearchAsync_EmptyKbIds_ReturnsEmpty()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var query = new KnowledgeQuery(db, _invoker);

        var result = await query.SearchAsync(new KnowledgeSearchRequest { KnowledgeBaseIds = [], Query = "x", TopK = 3 }, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task SearchAsync_NonexistentKb_ReturnsEmpty()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var query = new KnowledgeQuery(db, _invoker);

        var result = await query.SearchAsync(new KnowledgeSearchRequest { KnowledgeBaseIds = [999_999_999], Query = "x", TopK = 3 }, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task SearchAsync_RanksExactMatchFirst_WithSourceMetadata()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kbId = await NewKbAsync(db);
        await UploadAsync(db, kbId, "refund.txt", "退货政策为七天无理由退货");
        await UploadAsync(db, kbId, "warranty.txt", "保修期为一年含人为损坏除外");
        var query = new KnowledgeQuery(db, _invoker);

        var result = await query.SearchAsync(
            new KnowledgeSearchRequest { KnowledgeBaseIds = [kbId], Query = "退货政策为七天无理由退货", TopK = 3 },
            CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.NotEmpty(result.Data!);
        // 与查询文本相同的分块排最前，相似度≈1，且带来源文档名。
        Assert.Equal("退货政策为七天无理由退货", result.Data![0].Content);
        Assert.Equal("refund.txt", result.Data[0].DocumentName);
        Assert.True(result.Data[0].Score > 0.99);
    }

    [Fact]
    public async Task SearchAsync_TopK_LimitsResults()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kbId = await NewKbAsync(db);
        await UploadAsync(db, kbId, "a.txt", "第一篇文档内容甲");
        await UploadAsync(db, kbId, "b.txt", "第二篇文档内容乙");
        await UploadAsync(db, kbId, "c.txt", "第三篇文档内容丙");
        var query = new KnowledgeQuery(db, _invoker);

        var result = await query.SearchAsync(
            new KnowledgeSearchRequest { KnowledgeBaseIds = [kbId], Query = "文档内容", TopK = 2 },
            CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.True(result.Data!.Count <= 2);
    }

    [Fact]
    public async Task SearchAsync_ScoreThreshold_FiltersLowSimilarity()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kbId = await NewKbAsync(db);
        await UploadAsync(db, kbId, "a.txt", "精确匹配的查询文本内容");
        await UploadAsync(db, kbId, "b.txt", "完全不相关的另一段文字");
        var query = new KnowledgeQuery(db, _invoker);

        var result = await query.SearchAsync(
            new KnowledgeSearchRequest { KnowledgeBaseIds = [kbId], Query = "精确匹配的查询文本内容", TopK = 10, ScoreThreshold = 0.99 },
            CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.All(result.Data!, hit => Assert.True(hit.Score >= 0.99));
        Assert.Contains(result.Data!, hit => hit.Content == "精确匹配的查询文本内容");
    }

    [Fact]
    public async Task SearchAsync_EmbeddingFails_ReturnsFail()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kbId = await NewKbAsync(db);
        await UploadAsync(db, kbId, "a.txt", "一些内容");
        var failingQuery = new KnowledgeQuery(db, new FakeModelInvoker(dimensions: 1536, fail: true));

        var result = await failingQuery.SearchAsync(
            new KnowledgeSearchRequest { KnowledgeBaseIds = [kbId], Query = "一些内容", TopK = 3 },
            CancellationToken.None);

        Assert.Equal(7005, result.Code); // EmbeddingFailed
    }
}
