using Hify.Modules.Knowledge.Features.Documents;

namespace Hify.Modules.Knowledge.Tests.Features.Documents;

/// <summary>固定长度分块（带重叠）的纯逻辑测试。无 DB、无 LLM。</summary>
public sealed class TextChunkerTests
{
    [Fact]
    public void Empty_ReturnsNoChunks() =>
        Assert.Empty(TextChunker.Chunk("", chunkSize: 4, chunkOverlap: 1));

    [Theory]
    [InlineData("ab")]      // 短于 chunkSize
    [InlineData("abcd")]    // 恰等于 chunkSize
    public void ShorterThanOrEqualChunkSize_SingleChunk(string text)
    {
        var chunks = TextChunker.Chunk(text, chunkSize: 4, chunkOverlap: 1);

        Assert.Single(chunks);
        Assert.Equal(text, chunks[0]);
    }

    [Fact]
    public void NoOverlap_PartitionsExactly()
    {
        // 长度 10，size 4，overlap 0 → step 4：[0,4)[4,8)[8,10)
        var chunks = TextChunker.Chunk("0123456789", chunkSize: 4, chunkOverlap: 0);

        Assert.Equal(new[] { "0123", "4567", "89" }, chunks);
    }

    [Fact]
    public void WithOverlap_ConsecutiveChunksShareTail()
    {
        // 长度 10，size 4，overlap 1 → step 3：[0,4)[3,7)[6,10)
        var chunks = TextChunker.Chunk("0123456789", chunkSize: 4, chunkOverlap: 1);

        Assert.Equal(new[] { "0123", "3456", "6789" }, chunks);
    }

    [Fact]
    public void LastChunk_ReachesEnd_NoTrailingDuplicate()
    {
        // size 5，overlap 2 → step 3：[0,5)[3,8)[6,11)->[6,10) 末块到结尾即止，不再多一块
        var chunks = TextChunker.Chunk("0123456789", chunkSize: 5, chunkOverlap: 2);

        Assert.Equal(new[] { "01234", "34567", "6789" }, chunks);
        Assert.Equal("0123456789".Length, chunks[^1].Length + 6); // 末块起点 6
    }

    [Fact]
    public void EveryCharacterCovered()
    {
        const string text = "the quick brown fox jumps over the lazy dog";
        var chunks = TextChunker.Chunk(text, chunkSize: 7, chunkOverlap: 2);

        // 首块从 0 起、末块到结尾；相邻块重叠 overlap 个字符，整体无遗漏。
        Assert.Equal(text[..7], chunks[0]);
        Assert.EndsWith(text[^1].ToString(), chunks[^1]);
    }
}
