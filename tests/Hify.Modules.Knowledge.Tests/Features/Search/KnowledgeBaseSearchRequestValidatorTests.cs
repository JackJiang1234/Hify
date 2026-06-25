using Hify.Modules.Knowledge.Features.Search;

namespace Hify.Modules.Knowledge.Tests.Features.Search;

/// <summary>检索预览请求的格式与范围校验（无需 DB）。</summary>
public sealed class KnowledgeBaseSearchRequestValidatorTests
{
    private static readonly KnowledgeBaseSearchRequestValidator Validator = new();

    private static KnowledgeBaseSearchRequest Valid() => new() { Query = "退货政策", TopK = 3, ScoreThreshold = 0.5 };

    [Fact]
    public void Valid_Passes() => Assert.True(Validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("", false)]
    [InlineData("hi", true)]
    public void Query_Required(string query, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { Query = query }).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void TopK_InRange(int value, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { TopK = value }).IsValid);

    [Theory]
    [InlineData(-0.1, false)]
    [InlineData(0.0, true)]
    [InlineData(1.0, true)]
    [InlineData(1.1, false)]
    public void ScoreThreshold_InRange(double value, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { ScoreThreshold = value }).IsValid);
}
