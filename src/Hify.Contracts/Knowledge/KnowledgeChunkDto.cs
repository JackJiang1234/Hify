namespace Hify.Contracts.Knowledge;

/// <summary>检索命中的知识分块，含来源元数据以支持引用溯源。</summary>
public record KnowledgeChunkDto
{
    /// <summary>所属知识库 Id。</summary>
    public long KnowledgeBaseId { get; init; }

    /// <summary>来源文档 Id。</summary>
    public long DocumentId { get; init; }

    /// <summary>来源文档名（供"参考来源：xxx.txt"引用展示）。</summary>
    public string DocumentName { get; init; } = string.Empty;

    /// <summary>文档内分块序号。</summary>
    public int ChunkIndex { get; init; }

    /// <summary>分块文本。</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>相似度 <c>[0,1]</c>，已由余弦距离换算（<c>1 - distance</c>），越大越相关。</summary>
    public double Score { get; init; }
}
