using Hify.Contracts.Agent;
using Hify.Modules.Agent.Features.Agents;

namespace Hify.Modules.Agent.Tests.Features.Agents;

/// <summary>创建/更新请求的格式与范围校验（无需 DB）。引用存在性校验在服务层（方案 B）。</summary>
public sealed class AgentRequestValidatorTests
{
    private static readonly CreateAgentRequestValidator Validator = new();

    private static CreateAgentRequest Valid() => new()
    {
        Name = "assistant",
        Description = "a helpful agent",
        ModelId = 1,
        SystemPrompt = "you are helpful",
        MaxIterations = 5,
        RetrievalParams = new RetrievalParams { TopK = 3, ScoreThreshold = 0.5 },
        ModelParams = new ModelParams { Temperature = 0.7, TopP = 0.9, MaxTokens = 1024 },
        ToolIds = [10, 11],
        KnowledgeBaseIds = [20, 21],
    };

    [Fact]
    public void Valid_Passes() => Assert.True(Validator.Validate(Valid()).IsValid);

    [Fact]
    public void NullModelParams_UsesDefaults_Passes() =>
        Assert.True(Validator.Validate(Valid() with { ModelParams = null }).IsValid);

    [Fact]
    public void EmptyBindings_Passes() =>
        Assert.True(Validator.Validate(Valid() with { ToolIds = [], KnowledgeBaseIds = [] }).IsValid);

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
    public void ModelId_MustBePositive(long modelId, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { ModelId = modelId }).IsValid);

    [Fact]
    public void SystemPrompt_TooLong_Fails() =>
        Assert.False(Validator.Validate(Valid() with { SystemPrompt = new string('x', 8001) }).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void MaxIterations_InRange(int value, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { MaxIterations = value }).IsValid);

    [Theory]
    [InlineData(-0.1, false)]
    [InlineData(0.0, true)]
    [InlineData(2.0, true)]
    [InlineData(2.1, false)]
    public void Temperature_InRange(double value, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { ModelParams = new ModelParams { Temperature = value } }).IsValid);

    [Theory]
    [InlineData(-0.1, false)]
    [InlineData(0.0, true)]
    [InlineData(1.0, true)]
    [InlineData(1.1, false)]
    public void TopP_InRange(double value, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { ModelParams = new ModelParams { TopP = value } }).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void MaxTokens_MustBePositive(int value, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { ModelParams = new ModelParams { MaxTokens = value } }).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void TopK_InRange(int value, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { RetrievalParams = new RetrievalParams { TopK = value } }).IsValid);

    [Theory]
    [InlineData(-0.1, false)]
    [InlineData(0.0, true)]
    [InlineData(1.0, true)]
    [InlineData(1.1, false)]
    public void ScoreThreshold_InRange(double value, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { RetrievalParams = new RetrievalParams { TopK = 3, ScoreThreshold = value } }).IsValid);

    [Fact]
    public void ToolIds_NonPositive_Fails() =>
        Assert.False(Validator.Validate(Valid() with { ToolIds = [1, 0] }).IsValid);

    [Fact]
    public void ToolIds_Duplicates_Fails() =>
        Assert.False(Validator.Validate(Valid() with { ToolIds = [5, 5] }).IsValid);

    [Fact]
    public void KnowledgeBaseIds_Duplicates_Fails() =>
        Assert.False(Validator.Validate(Valid() with { KnowledgeBaseIds = [5, 5] }).IsValid);

    [Fact]
    public void ToolIds_TooMany_Fails() =>
        Assert.False(Validator.Validate(Valid() with { ToolIds = [.. Enumerable.Range(1, 51).Select(i => (long)i)] }).IsValid);
}
