using Hify.Contracts.Knowledge;
using Hify.Contracts.ModelProvider;
using Hify.Modules.Knowledge.Persistence;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Hify.Modules.Knowledge.Features.Search;

/// <summary>
/// <see cref="IKnowledgeQuery"/> 实现：跨指定知识库做 pgvector 余弦近邻检索（HNSW，带 LIMIT，禁全量排序）。
/// 按 embedding_model_id 分组——每种嵌入模型把 query 向量化一次、各组检索后合并，按相似度倒序取 TopK。
/// 相似度 = 1 - 余弦距离（<c>&lt;=&gt;</c>）；返回带来源文档名以支持引用溯源。
/// </summary>
internal sealed class KnowledgeQuery : IKnowledgeQuery
{
    private const int EmbeddingDimensions = 1536;

    private readonly KnowledgeDbContext _db;
    private readonly IModelInvoker _invoker;

    public KnowledgeQuery(KnowledgeDbContext db, IModelInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(invoker);
        _db = db;
        _invoker = invoker;
    }

    public async Task<Result<IReadOnlyList<KnowledgeChunkDto>>> SearchAsync(
        KnowledgeSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.KnowledgeBaseIds.Count == 0)
        {
            return Result<IReadOnlyList<KnowledgeChunkDto>>.Ok([]);
        }

        // 载入存在的库（含嵌入模型）；不存在/已删的库被跳过。
        var knowledgeBases = await _db.KnowledgeBases.AsNoTracking()
            .Where(kb => request.KnowledgeBaseIds.Contains(kb.Id))
            .Select(kb => new { kb.Id, kb.EmbeddingModelId })
            .ToListAsync(cancellationToken);
        if (knowledgeBases.Count == 0)
        {
            return Result<IReadOnlyList<KnowledgeChunkDto>>.Ok([]);
        }

        var hits = new List<KnowledgeChunkDto>();
        foreach (var group in knowledgeBases.GroupBy(kb => kb.EmbeddingModelId))
        {
            var embeddingModelId = group.Key;
            var kbIds = group.Select(kb => kb.Id).ToList();

            var queryVector = await EmbedQueryAsync(embeddingModelId, request.Query, cancellationToken);
            if (queryVector is null)
            {
                return Result<IReadOnlyList<KnowledgeChunkDto>>.Fail((int)KnowledgeErrorCode.EmbeddingFailed, "查询嵌入失败。");
            }

            // 近邻检索：HNSW 走 ORDER BY embedding <=> queryVector，Take 限制结果（禁全量排序）。
            // join document 同时过滤掉已软删文档的分块，并取来源文档名。
            var rows = await (
                from chunk in _db.Chunks.AsNoTracking()
                where kbIds.Contains(chunk.KnowledgeBaseId)
                join document in _db.Documents.AsNoTracking() on chunk.DocumentId equals document.Id
                orderby chunk.Embedding.CosineDistance(queryVector)
                select new
                {
                    chunk.KnowledgeBaseId,
                    chunk.DocumentId,
                    DocumentName = document.Name,
                    chunk.ChunkIndex,
                    chunk.Content,
                    Distance = chunk.Embedding.CosineDistance(queryVector),
                }).Take(request.TopK).ToListAsync(cancellationToken);

            hits.AddRange(rows.Select(row => new KnowledgeChunkDto
            {
                KnowledgeBaseId = row.KnowledgeBaseId,
                DocumentId = row.DocumentId,
                DocumentName = row.DocumentName,
                ChunkIndex = row.ChunkIndex,
                Content = row.Content,
                Score = 1 - row.Distance,
            }));
        }

        // 跨组合并：按相似度倒序取全局 TopK；ScoreThreshold>0 时过滤低相似度（0 表示不过滤）。
        var merged = hits
            .Where(hit => request.ScoreThreshold <= 0 || hit.Score >= request.ScoreThreshold)
            .OrderByDescending(hit => hit.Score)
            .Take(request.TopK)
            .ToList();

        return Result<IReadOnlyList<KnowledgeChunkDto>>.Ok(merged);
    }

    private async Task<Vector?> EmbedQueryAsync(long embeddingModelId, string query, CancellationToken cancellationToken)
    {
        var embed = await _invoker.EmbedAsync(embeddingModelId, new EmbeddingRequest { Inputs = [query] }, cancellationToken);
        if (embed.Code != 200 || embed.Data is null || embed.Data.Vectors.Count == 0)
        {
            return null;
        }

        var vector = embed.Data.Vectors[0];
        if (vector.Count != EmbeddingDimensions)
        {
            return null;
        }

        return new Vector(vector.ToArray());
    }
}
