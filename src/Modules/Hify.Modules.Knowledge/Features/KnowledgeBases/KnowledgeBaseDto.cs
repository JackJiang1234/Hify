namespace Hify.Modules.Knowledge.Features.KnowledgeBases;

/// <summary>
/// 知识库视图（模块内管理 API 返回用）。非跨模块契约，故不置于 Hify.Contracts。
/// </summary>
internal sealed record KnowledgeBaseDto
{
    /// <summary>主键。</summary>
    public long Id { get; init; }

    /// <summary>名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>嵌入模型 Id。</summary>
    public long EmbeddingModelId { get; init; }

    /// <summary>固定分块长度（字符数）。</summary>
    public int ChunkSize { get; init; }

    /// <summary>分块重叠长度（字符数）。</summary>
    public int ChunkOverlap { get; init; }

    /// <summary>库内文档数。<c>&gt; 0</c> 即已有分块，前端据此冻结嵌入模型/分块参数（服务端 7004 为最终权威）。</summary>
    public int DocumentCount { get; init; }

    /// <summary>创建时刻（epoch ms）。</summary>
    public long CreatedAt { get; init; }

    /// <summary>最后更新时刻（epoch ms）。</summary>
    public long UpdatedAt { get; init; }
}
