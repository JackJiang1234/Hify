using Hify.Modules.Knowledge.Domain;

namespace Hify.Modules.Knowledge.Features.Documents;

/// <summary>文档实体 → <see cref="DocumentDto"/> 映射。</summary>
internal static class DocumentMapping
{
    public static DocumentDto ToDto(Document entity) => new()
    {
        Id = entity.Id,
        KnowledgeBaseId = entity.KnowledgeBaseId,
        Name = entity.Name,
        FileType = entity.FileType,
        ContentHash = entity.ContentHash,
        Status = entity.Status,
        CharCount = entity.CharCount,
        ChunkCount = entity.ChunkCount,
        ErrorMessage = entity.ErrorMessage,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };
}
