using Hify.Shared.Persistence;

namespace Hify.Modules.Knowledge.Domain;

/// <summary>
/// 知识库实体。一个知识库 = 一组文档 + 一套固定的嵌入/分块配置。
/// <see cref="EmbeddingModelId"/> / <see cref="ChunkSize"/> / <see cref="ChunkOverlap"/> 一旦库内出现已嵌入分块即冻结
/// （换模型/改分块会使存量向量与新向量落在不同语义空间）；引用完整性由应用层维护，不建库级外键。
/// </summary>
internal sealed class KnowledgeBase : EntityBase
{
    /// <summary>名称（同一未删集合内唯一）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>嵌入模型 Id（-&gt; model_provider.model，须为 embedding 类型且维度 1536）。</summary>
    public long EmbeddingModelId { get; set; }

    /// <summary>固定分块长度（字符数）。</summary>
    public int ChunkSize { get; set; }

    /// <summary>分块重叠长度（字符数），须小于 <see cref="ChunkSize"/>。</summary>
    public int ChunkOverlap { get; set; }
}
