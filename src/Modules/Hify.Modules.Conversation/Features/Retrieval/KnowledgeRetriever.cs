using Hify.Contracts.Knowledge;

using Microsoft.Extensions.Logging;

namespace Hify.Modules.Conversation.Features.Retrieval;

/// <summary>
/// <see cref="IRetriever"/> 的生产实现：经 <see cref="IKnowledgeQuery"/>（Contracts）调用 Knowledge 模块（L2→L1）做 RAG 检索。
/// 检索失败（embedding 调用挂等）不应中断对话——记日志并降级为空片段，对话照常继续（降级策略由本适配器持有）。
/// </summary>
internal sealed class KnowledgeRetriever : IRetriever
{
    private readonly IKnowledgeQuery _knowledge;
    private readonly ILogger<KnowledgeRetriever> _logger;

    public KnowledgeRetriever(IKnowledgeQuery knowledge, ILogger<KnowledgeRetriever> logger)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentNullException.ThrowIfNull(logger);
        _knowledge = knowledge;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK,
        double scoreThreshold,
        CancellationToken cancellationToken)
    {
        if (knowledgeBaseIds.Count == 0)
        {
            return [];
        }

        var result = await _knowledge.SearchAsync(
            new KnowledgeSearchRequest
            {
                KnowledgeBaseIds = knowledgeBaseIds,
                Query = query,
                TopK = topK,
                ScoreThreshold = scoreThreshold,
            },
            cancellationToken);

        if (result.Code != 200 || result.Data is null)
        {
            // RAG 失败降级：不抛、不中断对话，本轮以无检索内容继续。
            _logger.LogWarning("知识检索失败（code={Code}），本轮降级为无检索内容。", result.Code);
            return [];
        }

        return result.Data.Select(chunk => new RetrievedChunk(chunk.Content, chunk.Score)).ToList();
    }
}
