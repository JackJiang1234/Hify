using Hify.Modules.Knowledge.Domain;

namespace Hify.Modules.Knowledge.Features.KnowledgeBases;

/// <summary>知识库实体 → <see cref="KnowledgeBaseDto"/> 映射。</summary>
internal static class KnowledgeBaseMapping
{
    public static KnowledgeBaseDto ToDto(KnowledgeBase entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        EmbeddingModelId = entity.EmbeddingModelId,
        ChunkSize = entity.ChunkSize,
        ChunkOverlap = entity.ChunkOverlap,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };
}
