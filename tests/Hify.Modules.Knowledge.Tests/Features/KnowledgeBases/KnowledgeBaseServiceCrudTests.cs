using Hify.Modules.Knowledge.Features.Documents;
using Hify.Modules.Knowledge.Features.KnowledgeBases;
using Hify.Modules.Knowledge.Persistence;
using Hify.Modules.Knowledge.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Knowledge.Tests.Features.KnowledgeBases;

/// <summary>
/// 知识库 CRUD（Get/List/Update/Delete）的真实库测试（连不上则跳过）。
/// 覆盖：更新冻结（决策 2，库有分块则嵌入模型/分块参数不可改 → 7004）、删除级联软删文档+分块。
/// </summary>
public sealed class KnowledgeBaseServiceCrudTests : IAsyncLifetime
{
    private const long EmbeddingModelId = 1;
    private const long WrongDimModelId = 2;
    private bool _available;

    public async Task InitializeAsync() => _available = await KnowledgeTestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static FakeModelProviderQuery Fake() =>
        new FakeModelProviderQuery()
            .Add(FakeModelProviderQuery.EmbeddingModel(EmbeddingModelId, dimensions: 1536))
            .Add(FakeModelProviderQuery.EmbeddingModel(WrongDimModelId, dimensions: 768));

    private static KnowledgeBaseService NewService(KnowledgeDbContext db) => new(db, Fake());

    private static async Task<KnowledgeBaseDto> CreateKbAsync(KnowledgeDbContext db, int chunkSize = 1000, int chunkOverlap = 100)
    {
        var created = await NewService(db).CreateAsync(
            new CreateKnowledgeBaseRequest
            {
                Name = $"it-{Guid.NewGuid():N}",
                EmbeddingModelId = EmbeddingModelId,
                ChunkSize = chunkSize,
                ChunkOverlap = chunkOverlap,
            },
            CancellationToken.None);
        return created.Data!;
    }

    private static UpdateKnowledgeBaseRequest UpdateFrom(KnowledgeBaseDto kb) => new()
    {
        Name = kb.Name,
        Description = kb.Description,
        EmbeddingModelId = kb.EmbeddingModelId,
        ChunkSize = kb.ChunkSize,
        ChunkOverlap = kb.ChunkOverlap,
    };

    private static async Task UploadOneDocAsync(KnowledgeDbContext db, long kbId)
    {
        var documents = new DocumentService(db, new FakeModelInvoker(dimensions: 1536));
        await documents.UploadAsync(
            new UploadDocumentRequest { KnowledgeBaseId = kbId, FileName = "doc.txt", Content = "一段用于生成分块的内容" },
            CancellationToken.None);
    }

    [Fact]
    public async Task Get_Existing_Returns200()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kb = await CreateKbAsync(db);

        var result = await NewService(db).GetAsync(kb.Id, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(kb.Id, result.Data!.Id);
    }

    [Fact]
    public async Task Get_Missing_Returns7001()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();

        var result = await NewService(db).GetAsync(999_999_999, CancellationToken.None);

        Assert.Equal(7001, result.Code);
    }

    [Fact]
    public async Task List_IncludesCreated()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kb = await CreateKbAsync(db);

        var result = await NewService(db).ListAsync(page: 1, size: 100, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Contains(result.Data!, item => item.Id == kb.Id);
    }

    [Fact]
    public async Task Update_NameAndDescription_Succeeds()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kb = await CreateKbAsync(db);
        var request = UpdateFrom(kb) with { Description = "改了描述" };

        var result = await NewService(db).UpdateAsync(kb.Id, request, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("改了描述", result.Data!.Description);
    }

    [Fact]
    public async Task Update_Missing_Returns7001()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var request = new UpdateKnowledgeBaseRequest { Name = "x", EmbeddingModelId = EmbeddingModelId, ChunkSize = 1000, ChunkOverlap = 100 };

        var result = await NewService(db).UpdateAsync(999_999_999, request, CancellationToken.None);

        Assert.Equal(7001, result.Code);
    }

    [Fact]
    public async Task Update_DuplicateName_Returns7009()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var first = await CreateKbAsync(db);
        var second = await CreateKbAsync(db);
        var request = UpdateFrom(second) with { Name = first.Name };

        var result = await NewService(db).UpdateAsync(second.Id, request, CancellationToken.None);

        Assert.Equal(7009, result.Code);
    }

    [Fact]
    public async Task Update_ChunkParams_NoChunks_Succeeds()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kb = await CreateKbAsync(db);
        var request = UpdateFrom(kb) with { ChunkSize = 500, ChunkOverlap = 50 };

        var result = await NewService(db).UpdateAsync(kb.Id, request, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(500, result.Data!.ChunkSize);
    }

    [Fact]
    public async Task Update_ChangeEmbeddingModel_WrongDim_Returns7003()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kb = await CreateKbAsync(db);
        var request = UpdateFrom(kb) with { EmbeddingModelId = WrongDimModelId }; // 768 维，无分块仍走维度校验

        var result = await NewService(db).UpdateAsync(kb.Id, request, CancellationToken.None);

        Assert.Equal(7003, result.Code);
    }

    [Fact]
    public async Task Update_FrozenFields_WithChunks_Returns7004()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kb = await CreateKbAsync(db);
        await UploadOneDocAsync(db, kb.Id); // 产生分块 → 冻结生效
        var request = UpdateFrom(kb) with { ChunkSize = 500 };

        var result = await NewService(db).UpdateAsync(kb.Id, request, CancellationToken.None);

        Assert.Equal(7004, result.Code); // KnowledgeBaseConfigLocked
    }

    [Fact]
    public async Task Delete_CascadeSoftDeletesDocumentsAndChunks()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kb = await CreateKbAsync(db);
        await UploadOneDocAsync(db, kb.Id);

        var deleted = await NewService(db).DeleteAsync(kb.Id, CancellationToken.None);
        Assert.Equal(200, deleted.Code);

        await using var verify = KnowledgeTestDb.NewContext();
        Assert.Equal(7001, (await NewService(verify).GetAsync(kb.Id, CancellationToken.None)).Code);
        Assert.Equal(0, await verify.Documents.CountAsync(d => d.KnowledgeBaseId == kb.Id));
        Assert.Equal(0, await verify.Chunks.CountAsync(c => c.KnowledgeBaseId == kb.Id));
    }

    [Fact]
    public async Task Delete_Missing_Returns7001()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();

        var result = await NewService(db).DeleteAsync(999_999_999, CancellationToken.None);

        Assert.Equal(7001, result.Code);
    }
}
