namespace Hify.Modules.Conversation.Features.Retrieval;

/// <summary>
/// <see cref="IRetriever"/> 的空实现：恒返回空片段列表。一期 RAG 占位（设计 C2=选项1）——
/// Knowledge 模块就绪前对话引擎不注入任何检索内容，但调用路径与契约已就位。
/// </summary>
internal sealed class NoopRetriever : IRetriever
{
    /// <inheritdoc />
    public Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RetrievedChunk>>([]);
}
