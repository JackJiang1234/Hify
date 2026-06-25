using FluentValidation;

namespace Hify.Modules.Knowledge.Features.KnowledgeBases;

/// <summary>创建知识库请求。嵌入模型的存在性与维度在服务层校验（须为 embedding 类型且维度 1536）。</summary>
internal sealed record CreateKnowledgeBaseRequest
{
    /// <summary>名称（唯一）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>嵌入模型 Id。</summary>
    public long EmbeddingModelId { get; init; }

    /// <summary>固定分块长度（字符数）。</summary>
    public int ChunkSize { get; init; } = 1000;

    /// <summary>分块重叠长度（字符数），须小于 <see cref="ChunkSize"/>。</summary>
    public int ChunkOverlap { get; init; } = 100;
}

/// <summary>建库请求的校验上下界。</summary>
internal static class KnowledgeBaseValidation
{
    /// <summary>分块长度下限（字符）：过短的块语义不足、检索噪声大。</summary>
    public const int MinChunkSize = 100;

    /// <summary>分块长度上限（字符）：过长易超嵌入模型输入限制、稀释相关性。</summary>
    public const int MaxChunkSize = 4000;
}

/// <summary>建库请求校验。</summary>
internal sealed class CreateKnowledgeBaseRequestValidator : AbstractValidator<CreateKnowledgeBaseRequest>
{
    public CreateKnowledgeBaseRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage("name 不能为空").MaximumLength(128).WithMessage("name 不超过 128 字符");
        RuleFor(request => request.Description).MaximumLength(512).WithMessage("description 不超过 512 字符");
        RuleFor(request => request.EmbeddingModelId).GreaterThan(0).WithMessage("embeddingModelId 非法");
        RuleFor(request => request.ChunkSize)
            .InclusiveBetween(KnowledgeBaseValidation.MinChunkSize, KnowledgeBaseValidation.MaxChunkSize)
            .WithMessage($"chunkSize 取值 [{KnowledgeBaseValidation.MinChunkSize}, {KnowledgeBaseValidation.MaxChunkSize}]");
        RuleFor(request => request.ChunkOverlap).GreaterThanOrEqualTo(0).WithMessage("chunkOverlap 不能为负");
        RuleFor(request => request)
            .Must(request => request.ChunkOverlap < request.ChunkSize)
            .WithMessage("chunkOverlap 须小于 chunkSize");
    }
}

/// <summary>
/// 更新知识库请求。name/description 随时可改；embeddingModelId / chunkSize / chunkOverlap 受冻结约束
/// （库内已有分块时不可改，服务层返回 7004）。校验上下界同建库。
/// </summary>
internal sealed record UpdateKnowledgeBaseRequest
{
    /// <summary>名称（唯一）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>嵌入模型 Id。</summary>
    public long EmbeddingModelId { get; init; }

    /// <summary>固定分块长度（字符数）。</summary>
    public int ChunkSize { get; init; } = 1000;

    /// <summary>分块重叠长度（字符数），须小于 <see cref="ChunkSize"/>。</summary>
    public int ChunkOverlap { get; init; } = 100;
}

/// <summary>更新建库请求校验（上下界同建库）。</summary>
internal sealed class UpdateKnowledgeBaseRequestValidator : AbstractValidator<UpdateKnowledgeBaseRequest>
{
    public UpdateKnowledgeBaseRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage("name 不能为空").MaximumLength(128).WithMessage("name 不超过 128 字符");
        RuleFor(request => request.Description).MaximumLength(512).WithMessage("description 不超过 512 字符");
        RuleFor(request => request.EmbeddingModelId).GreaterThan(0).WithMessage("embeddingModelId 非法");
        RuleFor(request => request.ChunkSize)
            .InclusiveBetween(KnowledgeBaseValidation.MinChunkSize, KnowledgeBaseValidation.MaxChunkSize)
            .WithMessage($"chunkSize 取值 [{KnowledgeBaseValidation.MinChunkSize}, {KnowledgeBaseValidation.MaxChunkSize}]");
        RuleFor(request => request.ChunkOverlap).GreaterThanOrEqualTo(0).WithMessage("chunkOverlap 不能为负");
        RuleFor(request => request)
            .Must(request => request.ChunkOverlap < request.ChunkSize)
            .WithMessage("chunkOverlap 须小于 chunkSize");
    }
}
