using Hify.Modules.Knowledge.Domain;
using Hify.Modules.Knowledge.Features.Documents;
using Hify.Modules.Knowledge.Features.KnowledgeBases;
using Hify.Modules.Knowledge.Persistence;
using Hify.Modules.Knowledge.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Knowledge.Tests.Features.Documents;

/// <summary>
/// 文档上传 + 同步处理的真实库测试（连不上则跳过）。上传即分块、嵌入（替身）、落库为 completed。
/// 校验：库存在、文件类型 txt、同库去重、嵌入失败不留半成品；分块/向量随文档写入 chunk 表。
/// </summary>
public sealed class DocumentServiceTests : IAsyncLifetime
{
    private const long EmbeddingModelId = 1;
    private bool _available;

    public async Task InitializeAsync() => _available = await KnowledgeTestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static FakeModelProviderQuery FakeWith1536Model() =>
        new FakeModelProviderQuery().Add(FakeModelProviderQuery.EmbeddingModel(EmbeddingModelId, dimensions: 1536));

    private static DocumentService NewService(KnowledgeDbContext db, bool embedFails = false) =>
        new(db, new FakeModelInvoker(dimensions: 1536, fail: embedFails));

    private static async Task<long> NewKnowledgeBaseAsync(KnowledgeDbContext db, int chunkSize = 1000, int chunkOverlap = 100)
    {
        var service = new KnowledgeBaseService(db, FakeWith1536Model());
        var created = await service.CreateAsync(
            new CreateKnowledgeBaseRequest
            {
                Name = $"it-{Guid.NewGuid():N}",
                EmbeddingModelId = EmbeddingModelId,
                ChunkSize = chunkSize,
                ChunkOverlap = chunkOverlap,
            },
            CancellationToken.None);
        return created.Data!.Id;
    }

    [Fact]
    public async Task UploadAsync_ValidTxt_ChunksEmbedsAndCompletes()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kbId = await NewKnowledgeBaseAsync(db);
        var service = NewService(db);
        const string content = "退货政策为 7 天无理由。";

        var result = await service.UploadAsync(
            new UploadDocumentRequest { KnowledgeBaseId = kbId, FileName = "manual.txt", Content = content },
            CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("txt", result.Data!.FileType);
        Assert.Equal(DocumentStatuses.Completed, result.Data.Status);
        Assert.Equal(content.Length, result.Data.CharCount);
        Assert.Equal(1, result.Data.ChunkCount); // 短文本 → 单块

        await using var verify = KnowledgeTestDb.NewContext();
        Assert.Equal(1, await verify.Chunks.CountAsync(c => c.DocumentId == result.Data.Id));
    }

    [Fact]
    public async Task UploadAsync_LongText_ProducesMultipleChunks()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        // 最小合法分块 100、重叠 20 → step 80。
        var kbId = await NewKnowledgeBaseAsync(db, chunkSize: 100, chunkOverlap: 20);
        var service = NewService(db);
        var content = new string('字', 250); // 长度 250：块 [0,100)[80,180)[160,250) = 3 块

        var result = await service.UploadAsync(
            new UploadDocumentRequest { KnowledgeBaseId = kbId, FileName = "long.txt", Content = content },
            CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(3, result.Data!.ChunkCount);

        await using var verify = KnowledgeTestDb.NewContext();
        var indices = await verify.Chunks.Where(c => c.DocumentId == result.Data.Id)
            .OrderBy(c => c.ChunkIndex).Select(c => c.ChunkIndex).ToListAsync();
        Assert.Equal(new[] { 0, 1, 2 }, indices);
    }

    [Fact]
    public async Task UploadAsync_EmbeddingFails_ReturnsEmbeddingFailed_PersistsNothing()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kbId = await NewKnowledgeBaseAsync(db);
        var service = NewService(db, embedFails: true);

        var result = await service.UploadAsync(
            new UploadDocumentRequest { KnowledgeBaseId = kbId, FileName = "manual.txt", Content = "嵌入会失败。" },
            CancellationToken.None);

        Assert.Equal(7005, result.Code); // EmbeddingFailed

        // 原子性：嵌入失败不留任何文档。
        await using var verify = KnowledgeTestDb.NewContext();
        Assert.Equal(0, await verify.Documents.CountAsync(d => d.KnowledgeBaseId == kbId));
    }

    [Fact]
    public async Task UploadAsync_KnowledgeBaseMissing_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var service = NewService(db);

        var result = await service.UploadAsync(
            new UploadDocumentRequest { KnowledgeBaseId = 999_999_999, FileName = "manual.txt", Content = "x" },
            CancellationToken.None);

        Assert.Equal(7001, result.Code); // KnowledgeBaseNotFound
    }

    [Theory]
    [InlineData("manual.pdf")]
    [InlineData("manual.docx")]
    [InlineData("manual")]
    public async Task UploadAsync_NonTxt_ReturnsUnsupportedFileType(string fileName)
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kbId = await NewKnowledgeBaseAsync(db);
        var service = NewService(db);

        var result = await service.UploadAsync(
            new UploadDocumentRequest { KnowledgeBaseId = kbId, FileName = fileName, Content = "x" },
            CancellationToken.None);

        Assert.Equal(7007, result.Code); // UnsupportedFileType
    }

    [Fact]
    public async Task UploadAsync_DuplicateContentSameKb_ReturnsDuplicate()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kbId = await NewKnowledgeBaseAsync(db);
        var service = NewService(db);
        const string content = "同样的内容只应入库一次。";
        await service.UploadAsync(new UploadDocumentRequest { KnowledgeBaseId = kbId, FileName = "a.txt", Content = content }, CancellationToken.None);

        // 同库、相同内容、不同文件名 —— 仍判重（按 content_hash）。
        var second = await service.UploadAsync(
            new UploadDocumentRequest { KnowledgeBaseId = kbId, FileName = "b.txt", Content = content },
            CancellationToken.None);

        Assert.Equal(7010, second.Code); // DocumentContentDuplicate
    }

    [Fact]
    public async Task UploadAsync_SameContentDifferentKb_BothSucceed()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var kb1 = await NewKnowledgeBaseAsync(db);
        var kb2 = await NewKnowledgeBaseAsync(db);
        var service = NewService(db);
        const string content = "跨库相同内容互不影响。";

        var first = await service.UploadAsync(new UploadDocumentRequest { KnowledgeBaseId = kb1, FileName = "a.txt", Content = content }, CancellationToken.None);
        var second = await service.UploadAsync(new UploadDocumentRequest { KnowledgeBaseId = kb2, FileName = "a.txt", Content = content }, CancellationToken.None);

        Assert.Equal(200, first.Code);
        Assert.Equal(200, second.Code);
    }
}
