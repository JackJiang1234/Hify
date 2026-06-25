using Hify.Modules.Knowledge.Features.KnowledgeBases;

namespace Hify.Modules.Knowledge.Tests.Features.KnowledgeBases;

/// <summary>更新建库请求的格式与范围校验（无需 DB）。冻结与嵌入模型校验在服务层。</summary>
public sealed class UpdateKnowledgeBaseRequestValidatorTests
{
    private static readonly UpdateKnowledgeBaseRequestValidator Validator = new();

    private static UpdateKnowledgeBaseRequest Valid() => new()
    {
        Name = "产品手册库",
        Description = "公司产品手册",
        EmbeddingModelId = 1,
        ChunkSize = 1000,
        ChunkOverlap = 100,
    };

    [Fact]
    public void Valid_Passes() => Assert.True(Validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("", false)]
    [InlineData("ok", true)]
    public void Name_Required(string name, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { Name = name }).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void EmbeddingModelId_MustBePositive(long modelId, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { EmbeddingModelId = modelId }).IsValid);

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, true)]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void ChunkSize_InRange(int value, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { ChunkSize = value, ChunkOverlap = 50 }).IsValid);

    [Fact]
    public void ChunkOverlap_NotLessThanChunkSize_Fails() =>
        Assert.False(Validator.Validate(Valid() with { ChunkSize = 500, ChunkOverlap = 500 }).IsValid);
}
