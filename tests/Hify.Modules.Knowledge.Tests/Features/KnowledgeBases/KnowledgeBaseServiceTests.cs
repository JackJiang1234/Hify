using Hify.Contracts.ModelProvider;
using Hify.Modules.Knowledge.Features.KnowledgeBases;
using Hify.Modules.Knowledge.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Knowledge.Tests.Features.KnowledgeBases;

/// <summary>
/// 知识库服务的真实库测试（连不上则跳过）。嵌入模型引用校验用内存替身 IModelProviderQuery。
/// 核心：建库时嵌入模型必须存在、为 embedding 类型、启用、且维度恰为 1536（决策 1）。
/// </summary>
public sealed class KnowledgeBaseServiceTests : IAsyncLifetime
{
    private const long EmbeddingModelId = 1;
    private bool _available;

    public async Task InitializeAsync() => _available = await KnowledgeTestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static FakeModelProviderQuery FakeWith1536Model() =>
        new FakeModelProviderQuery().Add(FakeModelProviderQuery.EmbeddingModel(EmbeddingModelId, dimensions: 1536));

    private static CreateKnowledgeBaseRequest NewKb(string name) => new()
    {
        Name = name,
        Description = "产品手册与政策文档",
        EmbeddingModelId = EmbeddingModelId,
        ChunkSize = 1000,
        ChunkOverlap = 100,
    };

    private static string UniqueName() => $"it-{Guid.NewGuid():N}";

    [Fact]
    public async Task CreateAsync_Valid1536Model_Persists()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var service = new KnowledgeBaseService(db, FakeWith1536Model());

        var result = await service.CreateAsync(NewKb(UniqueName()), CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(EmbeddingModelId, result.Data!.EmbeddingModelId);
        Assert.Equal(1000, result.Data.ChunkSize);
        Assert.Equal(100, result.Data.ChunkOverlap);

        await using var verify = KnowledgeTestDb.NewContext();
        Assert.True(await verify.KnowledgeBases.AnyAsync(kb => kb.Id == result.Data.Id));
    }

    [Fact]
    public async Task CreateAsync_WrongDimension_ReturnsDimensionMismatch()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        // 768 维（如 Ollama nomic-embed-text），不符合固定 1536。
        var fake = new FakeModelProviderQuery().Add(FakeModelProviderQuery.EmbeddingModel(EmbeddingModelId, dimensions: 768));
        var service = new KnowledgeBaseService(db, fake);

        var result = await service.CreateAsync(NewKb(UniqueName()), CancellationToken.None);

        Assert.Equal(7003, result.Code); // EmbeddingModelDimensionMismatch
    }

    [Fact]
    public async Task CreateAsync_ModelMissing_ReturnsEmbeddingModelInvalid()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var service = new KnowledgeBaseService(db, new FakeModelProviderQuery()); // 未预置任何模型

        var result = await service.CreateAsync(NewKb(UniqueName()), CancellationToken.None);

        Assert.Equal(7008, result.Code); // EmbeddingModelInvalid
    }

    [Fact]
    public async Task CreateAsync_ModelNotEmbedding_ReturnsEmbeddingModelInvalid()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var fake = new FakeModelProviderQuery().Add(new ModelDto
        {
            Id = EmbeddingModelId,
            ModelType = ModelTypes.Chat,
            Enabled = true,
            EmbeddingDimensions = 1536,
        });
        var service = new KnowledgeBaseService(db, fake);

        var result = await service.CreateAsync(NewKb(UniqueName()), CancellationToken.None);

        Assert.Equal(7008, result.Code);
    }

    [Fact]
    public async Task CreateAsync_ModelDisabled_ReturnsEmbeddingModelInvalid()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var fake = new FakeModelProviderQuery().Add(FakeModelProviderQuery.EmbeddingModel(EmbeddingModelId, enabled: false));
        var service = new KnowledgeBaseService(db, fake);

        var result = await service.CreateAsync(NewKb(UniqueName()), CancellationToken.None);

        Assert.Equal(7008, result.Code);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ReturnsNameConflict()
    {
        if (!_available)
        {
            return;
        }

        await using var db = KnowledgeTestDb.NewContext();
        var service = new KnowledgeBaseService(db, FakeWith1536Model());
        var name = UniqueName();
        await service.CreateAsync(NewKb(name), CancellationToken.None);

        var second = await service.CreateAsync(NewKb(name), CancellationToken.None);

        Assert.Equal(7009, second.Code); // KnowledgeBaseNameConflict
    }
}
