using Hify.Contracts.Knowledge;
using Hify.Modules.Conversation.Features.Retrieval;
using Hify.Modules.Conversation.Tests.Support;

using Microsoft.Extensions.Logging.Abstractions;

namespace Hify.Modules.Conversation.Tests.Features.Retrieval;

/// <summary>KnowledgeRetriever 适配器单测（替身 IKnowledgeQuery）：映射、参数透传、失败降级、空绑定短路。</summary>
public sealed class KnowledgeRetrieverTests
{
    private static KnowledgeRetriever NewRetriever(FakeKnowledgeQuery knowledge) =>
        new(knowledge, NullLogger<KnowledgeRetriever>.Instance);

    [Fact]
    public async Task RetrieveAsync_MapsChunks_ContentAndScore()
    {
        var knowledge = FakeKnowledgeQuery.Returning(
            new KnowledgeChunkDto { Content = "退货政策七天", Score = 0.95, DocumentName = "refund.txt" },
            new KnowledgeChunkDto { Content = "保修一年", Score = 0.80, DocumentName = "warranty.txt" });

        var chunks = await NewRetriever(knowledge).RetrieveAsync([1], "退货", topK: 3, scoreThreshold: 0.0, CancellationToken.None);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("退货政策七天", chunks[0].Content);
        Assert.Equal(0.95, chunks[0].Score);
    }

    [Fact]
    public async Task RetrieveAsync_ForwardsRequestParameters()
    {
        var knowledge = FakeKnowledgeQuery.Returning();

        await NewRetriever(knowledge).RetrieveAsync([7, 8], "问题", topK: 5, scoreThreshold: 0.42, CancellationToken.None);

        Assert.NotNull(knowledge.LastRequest);
        Assert.Equal(new long[] { 7, 8 }, knowledge.LastRequest!.KnowledgeBaseIds);
        Assert.Equal("问题", knowledge.LastRequest.Query);
        Assert.Equal(5, knowledge.LastRequest.TopK);
        Assert.Equal(0.42, knowledge.LastRequest.ScoreThreshold);
    }

    [Fact]
    public async Task RetrieveAsync_SearchFails_DegradesToEmpty()
    {
        var knowledge = FakeKnowledgeQuery.Failing(7005);

        var chunks = await NewRetriever(knowledge).RetrieveAsync([1], "查询", topK: 3, scoreThreshold: 0.0, CancellationToken.None);

        Assert.Empty(chunks); // 不抛异常，降级为空
    }

    [Fact]
    public async Task RetrieveAsync_NoBindings_ShortCircuitsWithoutQuerying()
    {
        var knowledge = FakeKnowledgeQuery.Returning();

        var chunks = await NewRetriever(knowledge).RetrieveAsync([], "查询", topK: 3, scoreThreshold: 0.0, CancellationToken.None);

        Assert.Empty(chunks);
        Assert.Null(knowledge.LastRequest); // 未触达 Knowledge
    }
}
