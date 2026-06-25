using Hify.Contracts.ModelProvider;
using Hify.Modules.Knowledge.Domain;
using Hify.Modules.Knowledge.Persistence;
using Hify.Shared.Pagination;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Knowledge.Features.KnowledgeBases;

/// <summary>
/// 知识库配置 CRUD 应用服务。可预期失败返回 <see cref="Result{T}"/>（7xxx），不抛异常。
/// 嵌入模型经 <see cref="IModelProviderQuery"/>（L0）校验：须存在、为 embedding 类型、启用、且维度恰为
/// <see cref="RequiredEmbeddingDimensions"/>（决策 1：维度锁死 1536，与 chunk.embedding vector(1536) 对齐）。
/// 更新冻结（决策 2）：库内已有分块后，嵌入模型与分块参数不可更改。
/// </summary>
internal sealed class KnowledgeBaseService
{
    // 向量列固定 vector(1536)，HNSW 索引据此建立；嵌入模型维度必须与之严格一致，否则插入向量即失败。
    private const int RequiredEmbeddingDimensions = 1536;

    private readonly KnowledgeDbContext _db;
    private readonly IModelProviderQuery _models;

    public KnowledgeBaseService(KnowledgeDbContext db, IModelProviderQuery models)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(models);
        _db = db;
        _models = models;
    }

    public async Task<Result<KnowledgeBaseDto>> CreateAsync(CreateKnowledgeBaseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _db.KnowledgeBases.AnyAsync(kb => kb.Name == request.Name, cancellationToken))
        {
            return Result<KnowledgeBaseDto>.Fail((int)KnowledgeErrorCode.KnowledgeBaseNameConflict, "知识库名称已存在。");
        }

        var modelError = await ValidateEmbeddingModelAsync(request.EmbeddingModelId, cancellationToken);
        if (modelError is not null)
        {
            return modelError;
        }

        var knowledgeBase = new KnowledgeBase
        {
            Name = request.Name,
            Description = request.Description,
            EmbeddingModelId = request.EmbeddingModelId,
            ChunkSize = request.ChunkSize,
            ChunkOverlap = request.ChunkOverlap,
        };

        _db.KnowledgeBases.Add(knowledgeBase);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // 唯一索引兜底并发冲突（校验与写入之间被他人抢先建同名库）。
            return Result<KnowledgeBaseDto>.Fail((int)KnowledgeErrorCode.KnowledgeBaseNameConflict, "知识库名称已存在。");
        }

        return Result<KnowledgeBaseDto>.Ok(KnowledgeBaseMapping.ToDto(knowledgeBase));
    }

    public async Task<Result<KnowledgeBaseDto>> GetAsync(long id, CancellationToken cancellationToken)
    {
        var knowledgeBase = await _db.KnowledgeBases.AsNoTracking()
            .FirstOrDefaultAsync(kb => kb.Id == id, cancellationToken);
        if (knowledgeBase is null)
        {
            return Result<KnowledgeBaseDto>.Fail((int)KnowledgeErrorCode.KnowledgeBaseNotFound, "知识库不存在。");
        }

        return Result<KnowledgeBaseDto>.Ok(KnowledgeBaseMapping.ToDto(knowledgeBase));
    }

    public async Task<PageResult<KnowledgeBaseDto>> ListAsync(int page, int size, CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Of(page, size);
        var query = _db.KnowledgeBases.AsNoTracking();

        var entities = await query.ApplyPage(pageRequest).ToListAsync(cancellationToken);
        var total = pageRequest.IsFirstPage ? await query.CountAsync(cancellationToken) : 0;

        var items = entities.Select(KnowledgeBaseMapping.ToDto).ToList();
        return PageResult<KnowledgeBaseDto>.Ok(items, total, pageRequest.Page, pageRequest.Size);
    }

    public async Task<Result<KnowledgeBaseDto>> UpdateAsync(long id, UpdateKnowledgeBaseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var knowledgeBase = await _db.KnowledgeBases.FirstOrDefaultAsync(kb => kb.Id == id, cancellationToken);
        if (knowledgeBase is null)
        {
            return Result<KnowledgeBaseDto>.Fail((int)KnowledgeErrorCode.KnowledgeBaseNotFound, "知识库不存在。");
        }

        if (knowledgeBase.Name != request.Name
            && await _db.KnowledgeBases.AnyAsync(other => other.Name == request.Name && other.Id != id, cancellationToken))
        {
            return Result<KnowledgeBaseDto>.Fail((int)KnowledgeErrorCode.KnowledgeBaseNameConflict, "知识库名称已存在。");
        }

        // 冻结（决策 2）：库内已有分块后，嵌入模型与分块参数不可更改（否则存量向量与新向量语义空间不一致）。
        var frozenFieldsChanged = request.EmbeddingModelId != knowledgeBase.EmbeddingModelId
            || request.ChunkSize != knowledgeBase.ChunkSize
            || request.ChunkOverlap != knowledgeBase.ChunkOverlap;
        if (frozenFieldsChanged && await _db.Chunks.AnyAsync(chunk => chunk.KnowledgeBaseId == id, cancellationToken))
        {
            return Result<KnowledgeBaseDto>.Fail(
                (int)KnowledgeErrorCode.KnowledgeBaseConfigLocked,
                "知识库已有分块，嵌入模型与分块参数不可更改（需新建库或清空重建）。");
        }

        // 仅当改动了嵌入模型才重新校验（含 1536 维约束）。
        if (request.EmbeddingModelId != knowledgeBase.EmbeddingModelId)
        {
            var modelError = await ValidateEmbeddingModelAsync(request.EmbeddingModelId, cancellationToken);
            if (modelError is not null)
            {
                return modelError;
            }
        }

        knowledgeBase.Name = request.Name;
        knowledgeBase.Description = request.Description;
        knowledgeBase.EmbeddingModelId = request.EmbeddingModelId;
        knowledgeBase.ChunkSize = request.ChunkSize;
        knowledgeBase.ChunkOverlap = request.ChunkOverlap;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<KnowledgeBaseDto>.Fail((int)KnowledgeErrorCode.KnowledgeBaseNameConflict, "知识库名称已存在。");
        }

        return Result<KnowledgeBaseDto>.Ok(KnowledgeBaseMapping.ToDto(knowledgeBase));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var knowledgeBase = await _db.KnowledgeBases.FirstOrDefaultAsync(kb => kb.Id == id, cancellationToken);
        if (knowledgeBase is null)
        {
            return Result<bool>.Fail((int)KnowledgeErrorCode.KnowledgeBaseNotFound, "知识库不存在。");
        }

        var now = _db.Clock.UtcNowEpochMs;

        // 级联软删：分块（量大、含向量，用 ExecuteUpdate 批量软删，不载入实体）+ 文档 + 库本身。同事务保证一致。
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.Chunks.Where(chunk => chunk.KnowledgeBaseId == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(chunk => chunk.DeletedAt, now)
                .SetProperty(chunk => chunk.UpdatedAt, now), cancellationToken);
        await _db.Documents.Where(document => document.KnowledgeBaseId == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(document => document.DeletedAt, now)
                .SetProperty(document => document.UpdatedAt, now), cancellationToken);

        _db.KnowledgeBases.Remove(knowledgeBase);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    // 嵌入模型是建库/改库唯一会校验存在性的引用（ModelProvider 为 L0，依赖合法）。
    private async Task<Result<KnowledgeBaseDto>?> ValidateEmbeddingModelAsync(long embeddingModelId, CancellationToken cancellationToken)
    {
        var result = await _models.GetModelAsync(embeddingModelId, cancellationToken);
        if (result.Code != 200 || result.Data is null)
        {
            return Result<KnowledgeBaseDto>.Fail((int)KnowledgeErrorCode.EmbeddingModelInvalid, "引用的嵌入模型不存在。");
        }

        var model = result.Data;
        if (model.ModelType != ModelTypes.Embedding)
        {
            return Result<KnowledgeBaseDto>.Fail((int)KnowledgeErrorCode.EmbeddingModelInvalid, "引用的模型不是 embedding 类型。");
        }

        if (!model.Enabled)
        {
            return Result<KnowledgeBaseDto>.Fail((int)KnowledgeErrorCode.EmbeddingModelInvalid, "引用的嵌入模型已停用。");
        }

        if (model.EmbeddingDimensions != RequiredEmbeddingDimensions)
        {
            return Result<KnowledgeBaseDto>.Fail(
                (int)KnowledgeErrorCode.EmbeddingModelDimensionMismatch,
                $"嵌入模型维度须为 {RequiredEmbeddingDimensions}，当前为 {model.EmbeddingDimensions}。");
        }

        return null;
    }
}
