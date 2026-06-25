namespace Hify.Modules.Conversation.Features.Retrieval;

/// <summary>
/// RAG 检索 seam。对话引擎装配上下文时经此取回相关知识片段。
/// 真实实现 <see cref="KnowledgeRetriever"/> 经 Contracts 调 Knowledge 模块（L2→L1），检索失败时降级为空；
/// <see cref="NoopRetriever"/> 为恒空实现（测试桩 / 无 Knowledge 时回退）。装配主流程不感知具体实现。
/// </summary>
internal interface IRetriever
{
    /// <summary>按知识库与查询检索相关片段。无绑定或检索不可用时返回空列表。</summary>
    /// <param name="knowledgeBaseIds">Agent 绑定的知识库 Id 列表。</param>
    /// <param name="query">用户查询文本。</param>
    /// <param name="topK">返回片段上限。</param>
    /// <param name="scoreThreshold">相似度阈值 <c>[0,1]</c>，低于该值的片段被丢弃；0 表示不过滤。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK,
        double scoreThreshold,
        CancellationToken cancellationToken);
}

/// <summary>检索到的知识片段。</summary>
/// <param name="Content">片段文本。</param>
/// <param name="Score">相关性得分（越大越相关）。</param>
internal sealed record RetrievedChunk(string Content, double Score);
