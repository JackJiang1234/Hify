namespace Hify.Contracts.Agent;

/// <summary>
/// Agent 的 RAG 检索参数（Agent 级，落库为 jsonb）。对该 Agent 绑定的所有知识库统一生效。
/// </summary>
public record RetrievalParams
{
    /// <summary>检索返回的最相近分块数，取值 <c>[1, 20]</c>，默认 3。</summary>
    public int TopK { get; init; } = 3;

    /// <summary>相似度阈值，取值 <c>[0.0, 1.0]</c>，默认 0（不过滤）。低于该分值的分块被丢弃。</summary>
    public double ScoreThreshold { get; init; }
}
