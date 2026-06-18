using FluentValidation;

using Hify.Contracts.ModelProvider;

namespace Hify.Modules.ModelProvider.Features.Models;

/// <summary>创建模型请求（手动录入）。新模型默认非默认模型，经 set-default 单独设置。</summary>
internal sealed record CreateModelRequest
{
    /// <summary>模型标识（API 侧名称，如 gpt-4o）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>展示名称。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>模型类型，见 <see cref="ModelTypes"/>。</summary>
    public string ModelType { get; init; } = string.Empty;

    /// <summary>上下文窗口 token 数。</summary>
    public long ContextWindow { get; init; }

    /// <summary>单次最大输出 token 数。</summary>
    public long MaxOutputTokens { get; init; }

    /// <summary>嵌入维度（嵌入模型须为 1536）。</summary>
    public int EmbeddingDimensions { get; init; }

    /// <summary>是否支持流式。</summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>是否支持工具调用。</summary>
    public bool SupportsTools { get; init; }

    /// <summary>是否支持视觉。</summary>
    public bool SupportsVision { get; init; }

    /// <summary>展示排序。</summary>
    public int SortOrder { get; init; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>更新模型请求。</summary>
internal sealed record UpdateModelRequest
{
    /// <summary>模型标识。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>展示名称。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>模型类型，见 <see cref="ModelTypes"/>。</summary>
    public string ModelType { get; init; } = string.Empty;

    /// <summary>上下文窗口 token 数。</summary>
    public long ContextWindow { get; init; }

    /// <summary>单次最大输出 token 数。</summary>
    public long MaxOutputTokens { get; init; }

    /// <summary>嵌入维度（嵌入模型须为 1536）。</summary>
    public int EmbeddingDimensions { get; init; }

    /// <summary>是否支持流式。</summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>是否支持工具调用。</summary>
    public bool SupportsTools { get; init; }

    /// <summary>是否支持视觉。</summary>
    public bool SupportsVision { get; init; }

    /// <summary>展示排序。</summary>
    public int SortOrder { get; init; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>模型类型常量校验。pgvector 维度固定 1536，嵌入模型须匹配。</summary>
internal static class ModelValidation
{
    /// <summary>pgvector 固定向量维度。</summary>
    public const int RequiredEmbeddingDimensions = 1536;

    private static readonly string[] KnownModelTypes = [ModelTypes.Chat, ModelTypes.Embedding];

    public static bool BeKnownModelType(string value) => Array.IndexOf(KnownModelTypes, value) >= 0;
}

/// <summary>创建模型校验。</summary>
internal sealed class CreateModelRequestValidator : AbstractValidator<CreateModelRequest>
{
    public CreateModelRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage("name 不能为空").MaximumLength(128).WithMessage("name 不超过 128 字符");
        RuleFor(request => request.ModelType).Must(ModelValidation.BeKnownModelType).WithMessage("modelType 非法（chat | embedding）");
        RuleFor(request => request.ContextWindow).GreaterThanOrEqualTo(0);
        RuleFor(request => request.MaxOutputTokens).GreaterThanOrEqualTo(0);
        RuleFor(request => request.EmbeddingDimensions)
            .Equal(ModelValidation.RequiredEmbeddingDimensions)
            .When(request => request.ModelType == ModelTypes.Embedding)
            .WithMessage("嵌入模型维度须为 1536（与 pgvector 固定维度一致）");
    }
}

/// <summary>更新模型校验。</summary>
internal sealed class UpdateModelRequestValidator : AbstractValidator<UpdateModelRequest>
{
    public UpdateModelRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage("name 不能为空").MaximumLength(128).WithMessage("name 不超过 128 字符");
        RuleFor(request => request.ModelType).Must(ModelValidation.BeKnownModelType).WithMessage("modelType 非法（chat | embedding）");
        RuleFor(request => request.ContextWindow).GreaterThanOrEqualTo(0);
        RuleFor(request => request.MaxOutputTokens).GreaterThanOrEqualTo(0);
        RuleFor(request => request.EmbeddingDimensions)
            .Equal(ModelValidation.RequiredEmbeddingDimensions)
            .When(request => request.ModelType == ModelTypes.Embedding)
            .WithMessage("嵌入模型维度须为 1536（与 pgvector 固定维度一致）");
    }
}
