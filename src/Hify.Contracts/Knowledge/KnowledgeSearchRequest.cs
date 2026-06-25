namespace Hify.Contracts.Knowledge;

/// <summary>RAG 检索请求（供应商/存储无关）。</summary>
public record KnowledgeSearchRequest
{
    /// <summary>检索范围：Agent 绑定的知识库 Id 列表。空列表返回空结果。</summary>
    public IReadOnlyList<long> KnowledgeBaseIds { get; init; } = [];

    /// <summary>用户查询文本。</summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>返回分块上限（跨所有库合并后的总数），取值 <c>[1, 20]</c>。</summary>
    public int TopK { get; init; } = 3;

    /// <summary>相似度阈值 <c>[0.0, 1.0]</c>，低于该值的分块丢弃；0 表示不过滤。</summary>
    public double ScoreThreshold { get; init; }
}
