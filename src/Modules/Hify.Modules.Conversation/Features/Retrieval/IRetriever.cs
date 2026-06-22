namespace Hify.Modules.Conversation.Features.Retrieval;

/// <summary>
/// RAG 检索 seam。对话引擎装配上下文时经此取回相关知识片段。
/// 一期（设计决策 C2=选项1）Knowledge 模块尚未落地，默认实现 <see cref="NoopRetriever"/> 恒返回空；
/// 待 Knowledge 就绪后替换为真实实现，无需改动对话引擎主流程。
/// </summary>
internal interface IRetriever
{
    /// <summary>按知识库与查询检索相关片段。无绑定或检索不可用时返回空列表。</summary>
    /// <param name="knowledgeBaseIds">Agent 绑定的知识库 Id 列表。</param>
    /// <param name="query">用户查询文本。</param>
    /// <param name="topK">返回片段上限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK,
        CancellationToken cancellationToken);
}

/// <summary>检索到的知识片段。</summary>
/// <param name="Content">片段文本。</param>
/// <param name="Score">相关性得分（越大越相关）。</param>
internal sealed record RetrievedChunk(string Content, double Score);
