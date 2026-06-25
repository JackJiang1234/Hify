using Hify.Modules.Knowledge.Features.Documents;
using Hify.Modules.Knowledge.Features.KnowledgeBases;
using Hify.Modules.Knowledge.Persistence;
using Hify.Modules.Knowledge.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Knowledge.Tests.Features.Documents;

/// <summary>文档 List/Get/Delete 的真实库测试（连不上则跳过）。删除级联软删其分块。</summary>
public sealed class DocumentServiceCrudTests : IAsyncLifetime
{
    private const long EmbeddingModelId = 1;
    private bool _available;

    public async Task InitializeAsync() => _available = await KnowledgeTestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static FakeModelProviderQuery FakeWith1536Model() =>
        new FakeModelProviderQuery().Add(FakeModelProviderQuery.EmbeddingModel(EmbeddingModelId, dimensions: 1536));

    private static DocumentService NewDocuments(KnowledgeDbContext db) => new(db, new FakeModelInvoker(dimensions: 1536));

    private static async Task<long> NewKbAsync(KnowledgeDbContext db)
    {
        var created = await new KnowledgeBaseService(db, FakeWith1536Model()).CreateAsync(
            new CreateKnowledgeBaseRequest { Name = $"it-{Guid.NewGuid():N}", EmbeddingModelId = EmbeddingModelId, ChunkSize = 1000, ChunkOverlap = 100 },
            CancellationToken.None);
        return created.Data!.Id;
    }

    private static Task<long> UploadAsync(KnowledgeDbContext db, long kbId, string fileName, string content) =>
        NewDocuments(db).UploadAsync(
                new UploadDocumentRequest { KnowledgeBaseId = kbId, FileName = fileName, Content = content },
                CancellationToken.None)
            .ContinueWith(t => t.Result.Data!.Id);

    [Fact]
    public async Task List_ReturnsDocsForKb()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kbId = await NewKbAsync(db);
        await UploadAsync(db, kbId, "a.txt", "第一篇");
        await UploadAsync(db, kbId, "b.txt", "第二篇");

        var result = await NewDocuments(db).ListAsync(kbId, page: 1, size: 100, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task Get_Existing_Returns200()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kbId = await NewKbAsync(db);
        var docId = await UploadAsync(db, kbId, "a.txt", "内容甲");

        var result = await NewDocuments(db).GetAsync(kbId, docId, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(docId, result.Data!.Id);
    }

    [Fact]
    public async Task Get_WrongKb_Returns7002()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kb1 = await NewKbAsync(db);
        var kb2 = await NewKbAsync(db);
        var docId = await UploadAsync(db, kb1, "a.txt", "内容甲");

        var result = await NewDocuments(db).GetAsync(kb2, docId, CancellationToken.None);

        Assert.Equal(7002, result.Code); // 文档不属于该库
    }

    [Fact]
    public async Task Get_Missing_Returns7002()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kbId = await NewKbAsync(db);

        var result = await NewDocuments(db).GetAsync(kbId, 999_999_999, CancellationToken.None);

        Assert.Equal(7002, result.Code);
    }

    [Fact]
    public async Task Delete_CascadeSoftDeletesChunks()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kbId = await NewKbAsync(db);
        var docId = await UploadAsync(db, kbId, "a.txt", "内容甲");

        var deleted = await NewDocuments(db).DeleteAsync(kbId, docId, CancellationToken.None);
        Assert.Equal(200, deleted.Code);

        await using var verify = KnowledgeTestDb.NewContext();
        Assert.Equal(7002, (await NewDocuments(verify).GetAsync(kbId, docId, CancellationToken.None)).Code);
        Assert.Equal(0, await verify.Chunks.CountAsync(c => c.DocumentId == docId));
    }

    [Fact]
    public async Task Delete_Missing_Returns7002()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kbId = await NewKbAsync(db);

        var result = await NewDocuments(db).DeleteAsync(kbId, 999_999_999, CancellationToken.None);

        Assert.Equal(7002, result.Code);
    }
}
