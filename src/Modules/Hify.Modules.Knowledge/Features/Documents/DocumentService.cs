using System.Security.Cryptography;
using System.Text;

using Hify.Contracts.ModelProvider;
using Hify.Modules.Knowledge.Domain;
using Hify.Modules.Knowledge.Persistence;
using Hify.Shared.Pagination;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

using Pgvector;

namespace Hify.Modules.Knowledge.Features.Documents;

/// <summary>
/// 文档上传 + 同步处理流水线。可预期失败返回 <see cref="Result{T}"/>（7xxx），不抛异常。
/// 一期仅 TXT、同步处理（20-50 人内部规模够用）：校验库存在/类型/去重 → 固定长度分块 →
/// 经 <see cref="IModelInvoker"/> 批量嵌入 → 事务内写 document(completed) + chunk(向量)。
/// 全程原子：嵌入失败则不留半成品（不存原文，决策 3，故重试靠重新上传）。
/// </summary>
internal sealed class DocumentService
{
    private const string TxtFileType = "txt";

    // 与 chunk.embedding vector(1536) 及建库时的维度校验一致；防嵌入模型返回异常维度污染插入。
    private const int EmbeddingDimensions = 1536;

    private readonly KnowledgeDbContext _db;
    private readonly IModelInvoker _invoker;

    public DocumentService(KnowledgeDbContext db, IModelInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(invoker);
        _db = db;
        _invoker = invoker;
    }

    public async Task<Result<DocumentDto>> UploadAsync(UploadDocumentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fileType = ResolveFileType(request.FileName);
        if (fileType != TxtFileType)
        {
            return Result<DocumentDto>.Fail((int)KnowledgeErrorCode.UnsupportedFileType, "一期仅支持 TXT 文档。");
        }

        var knowledgeBase = await _db.KnowledgeBases.AsNoTracking()
            .FirstOrDefaultAsync(kb => kb.Id == request.KnowledgeBaseId, cancellationToken);
        if (knowledgeBase is null)
        {
            return Result<DocumentDto>.Fail((int)KnowledgeErrorCode.KnowledgeBaseNotFound, "知识库不存在。");
        }

        var contentHash = ComputeHash(request.Content);
        if (await _db.Documents.AnyAsync(
                document => document.KnowledgeBaseId == request.KnowledgeBaseId && document.ContentHash == contentHash,
                cancellationToken))
        {
            return Result<DocumentDto>.Fail((int)KnowledgeErrorCode.DocumentContentDuplicate, "该内容的文档已存在于此知识库。");
        }

        // 分块：content 非空（已校验），故至少一块。
        var pieces = TextChunker.Chunk(request.Content, knowledgeBase.ChunkSize, knowledgeBase.ChunkOverlap);

        // 批量嵌入（一次调用，顺序与 pieces 一一对应）。
        var embedResult = await _invoker.EmbedAsync(
            knowledgeBase.EmbeddingModelId,
            new EmbeddingRequest { Inputs = pieces },
            cancellationToken);
        if (embedResult.Code != 200 || embedResult.Data is null)
        {
            return Result<DocumentDto>.Fail((int)KnowledgeErrorCode.EmbeddingFailed, "嵌入调用失败。");
        }

        var vectors = embedResult.Data.Vectors;
        if (vectors.Count != pieces.Count || vectors.Any(vector => vector.Count != EmbeddingDimensions))
        {
            return Result<DocumentDto>.Fail((int)KnowledgeErrorCode.EmbeddingFailed, "嵌入结果数量或维度与预期不符。");
        }

        return await PersistAsync(request, fileType, contentHash, pieces, vectors, cancellationToken);
    }

    public async Task<PageResult<DocumentDto>> ListAsync(long knowledgeBaseId, int page, int size, CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Of(page, size);
        var query = _db.Documents.AsNoTracking().Where(document => document.KnowledgeBaseId == knowledgeBaseId);

        var entities = await query.ApplyPage(pageRequest).ToListAsync(cancellationToken);
        var total = pageRequest.IsFirstPage ? await query.CountAsync(cancellationToken) : 0;

        var items = entities.Select(DocumentMapping.ToDto).ToList();
        return PageResult<DocumentDto>.Ok(items, total, pageRequest.Page, pageRequest.Size);
    }

    public async Task<Result<DocumentDto>> GetAsync(long knowledgeBaseId, long documentId, CancellationToken cancellationToken)
    {
        var document = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.KnowledgeBaseId == knowledgeBaseId, cancellationToken);
        if (document is null)
        {
            return Result<DocumentDto>.Fail((int)KnowledgeErrorCode.DocumentNotFound, "文档不存在。");
        }

        return Result<DocumentDto>.Ok(DocumentMapping.ToDto(document));
    }

    public async Task<Result<bool>> DeleteAsync(long knowledgeBaseId, long documentId, CancellationToken cancellationToken)
    {
        var document = await _db.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.KnowledgeBaseId == knowledgeBaseId, cancellationToken);
        if (document is null)
        {
            return Result<bool>.Fail((int)KnowledgeErrorCode.DocumentNotFound, "文档不存在。");
        }

        var now = _db.Clock.UtcNowEpochMs;

        // 级联软删：先批量软删分块（含向量，用 ExecuteUpdate 不载入实体），再软删文档本身。同事务保证一致。
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.Chunks.Where(chunk => chunk.DocumentId == documentId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(chunk => chunk.DeletedAt, now)
                .SetProperty(chunk => chunk.UpdatedAt, now), cancellationToken);

        _db.Documents.Remove(document);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    // 事务内写 document + chunks，保证「文档与其分块」要么全在、要么全不在。
    private async Task<Result<DocumentDto>> PersistAsync(
        UploadDocumentRequest request,
        string fileType,
        string contentHash,
        IReadOnlyList<string> pieces,
        IReadOnlyList<IReadOnlyList<float>> vectors,
        CancellationToken cancellationToken)
    {
        var document = new Document
        {
            KnowledgeBaseId = request.KnowledgeBaseId,
            Name = request.FileName,
            FileType = fileType,
            ContentHash = contentHash,
            Status = DocumentStatuses.Completed,
            CharCount = request.Content.Length,
            ChunkCount = pieces.Count,
        };

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.Documents.Add(document);
            await _db.SaveChangesAsync(cancellationToken);

            for (var index = 0; index < pieces.Count; index++)
            {
                _db.Chunks.Add(new Chunk
                {
                    DocumentId = document.Id,
                    KnowledgeBaseId = request.KnowledgeBaseId,
                    ChunkIndex = index,
                    Content = pieces[index],
                    Embedding = new Vector(vectors[index].ToArray()),
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            // 唯一索引 (knowledge_base_id, content_hash) 兜底并发：校验与写入之间被他人抢先上传同内容。
            return Result<DocumentDto>.Fail((int)KnowledgeErrorCode.DocumentContentDuplicate, "该内容的文档已存在于此知识库。");
        }

        return Result<DocumentDto>.Ok(DocumentMapping.ToDto(document));
    }

    // 由文件名扩展名判定类型，统一小写；无扩展名得空串（非 txt，将被拒）。
    private static string ResolveFileType(string fileName) =>
        Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();

    private static string ComputeHash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}
