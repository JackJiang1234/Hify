using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Features.Models;

namespace Hify.Modules.ModelProvider.Tests.Features.Models;

/// <summary>模型请求校验（无需 DB）：类型枚举、嵌入维度须 1536。</summary>
public sealed class ModelRequestValidatorTests
{
    private static readonly CreateModelRequestValidator Validator = new();

    [Fact]
    public void Chat_Valid_Passes()
    {
        var request = new CreateModelRequest { Name = "gpt-4o", ModelType = ModelTypes.Chat, ContextWindow = 128000 };

        Assert.True(Validator.Validate(request).IsValid);
    }

    [Fact]
    public void Embedding_With1536_Passes()
    {
        var request = new CreateModelRequest { Name = "text-embedding-3-small", ModelType = ModelTypes.Embedding, EmbeddingDimensions = 1536 };

        Assert.True(Validator.Validate(request).IsValid);
    }

    [Fact]
    public void Embedding_WrongDimension_Fails()
    {
        var request = new CreateModelRequest { Name = "weird-embed", ModelType = ModelTypes.Embedding, EmbeddingDimensions = 3072 };

        Assert.False(Validator.Validate(request).IsValid);
    }

    [Fact]
    public void UnknownModelType_Fails()
    {
        var request = new CreateModelRequest { Name = "x", ModelType = "audio" };

        Assert.False(Validator.Validate(request).IsValid);
    }

    [Fact]
    public void EmptyName_Fails()
    {
        var request = new CreateModelRequest { Name = "", ModelType = ModelTypes.Chat };

        Assert.False(Validator.Validate(request).IsValid);
    }
}
