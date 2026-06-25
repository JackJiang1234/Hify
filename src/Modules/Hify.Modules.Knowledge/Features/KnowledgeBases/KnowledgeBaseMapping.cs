using Hify.Modules.Knowledge.Domain;

namespace Hify.Modules.Knowledge.Features.KnowledgeBases;

/// <summary>知识库实体 → <see cref="KnowledgeBaseDto"/> 映射。</summary>
internal static class KnowledgeBaseMapping
{
    /// <param name="entity">知识库实体。</param>
    /// <param name="documentCount">库内文档数（由服务层统计）。</param>
    public static KnowledgeBaseDto ToDto(KnowledgeBase entity, int documentCount) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        EmbeddingModelId = entity.EmbeddingModelId,
        ChunkSize = entity.ChunkSize,
        ChunkOverlap = entity.ChunkOverlap,
        DocumentCount = documentCount,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };
}
