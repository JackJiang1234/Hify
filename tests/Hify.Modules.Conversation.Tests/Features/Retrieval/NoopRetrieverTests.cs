using Hify.Modules.Conversation.Features.Retrieval;

namespace Hify.Modules.Conversation.Tests.Features.Retrieval;

/// <summary>一期 RAG 空 seam：无论是否有知识库绑定，恒返回空片段，且不抛异常。</summary>
public sealed class NoopRetrieverTests
{
    [Fact]
    public async Task RetrieveAsync_WithBindings_ReturnsEmpty()
    {
        var retriever = new NoopRetriever();

        var chunks = await retriever.RetrieveAsync([1, 2, 3], "任意查询", topK: 5, scoreThreshold: 0.0, CancellationToken.None);

        Assert.Empty(chunks);
    }

    [Fact]
    public async Task RetrieveAsync_NoBindings_ReturnsEmpty()
    {
        var retriever = new NoopRetriever();

        var chunks = await retriever.RetrieveAsync([], "查询", topK: 3, scoreThreshold: 0.0, CancellationToken.None);

        Assert.Empty(chunks);
    }
}
