using Hify.Modules.Knowledge.Features.KnowledgeBases;

namespace Hify.Modules.Knowledge.Tests.Features.KnowledgeBases;

/// <summary>建库请求的格式与范围校验（无需 DB）。嵌入模型存在性与维度在服务层校验。</summary>
public sealed class CreateKnowledgeBaseRequestValidatorTests
{
    private static readonly CreateKnowledgeBaseRequestValidator Validator = new();

    private static CreateKnowledgeBaseRequest Valid() => new()
    {
        Name = "产品手册库",
        Description = "公司产品手册与政策文档",
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

    [Fact]
    public void Name_TooLong_Fails() =>
        Assert.False(Validator.Validate(Valid() with { Name = new string('x', 129) }).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void EmbeddingModelId_MustBePositive(long modelId, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { EmbeddingModelId = modelId }).IsValid);

    [Theory]
    [InlineData(99, false)]    // < MinChunkSize
    [InlineData(100, true)]
    [InlineData(4000, true)]
    [InlineData(4001, false)]  // > MaxChunkSize
    public void ChunkSize_InRange(int value, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { ChunkSize = value, ChunkOverlap = 50 }).IsValid);

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(100, true)]
    public void ChunkOverlap_NonNegative(int value, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { ChunkOverlap = value }).IsValid);

    [Fact]
    public void ChunkOverlap_NotLessThanChunkSize_Fails() =>
        Assert.False(Validator.Validate(Valid() with { ChunkSize = 500, ChunkOverlap = 500 }).IsValid);
}
