namespace Hify.Modules.Conversation.Features.Retrieval;

/// <summary>
/// <see cref="IRetriever"/> 的空实现：恒返回空片段列表。作为测试桩与"无 Knowledge"回退；
/// 生产默认实现为 <see cref="KnowledgeRetriever"/>。
/// </summary>
internal sealed class NoopRetriever : IRetriever
{
    /// <inheritdoc />
    public Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK,
        double scoreThreshold,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RetrievedChunk>>([]);
}
