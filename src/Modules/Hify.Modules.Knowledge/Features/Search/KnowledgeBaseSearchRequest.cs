using FluentValidation;

namespace Hify.Modules.Knowledge.Features.Search;

/// <summary>
/// 单库检索预览请求（HTTP，管理员调参用）。库 Id 取自路由；与跨模块契约
/// <c>Hify.Contracts.Knowledge.KnowledgeSearchRequest</c> 区分，后者承载多库与向量化细节。
/// </summary>
internal sealed record KnowledgeBaseSearchRequest
{
    /// <summary>查询文本。</summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>返回分块上限，取值 <c>[1, 20]</c>。</summary>
    public int TopK { get; init; } = 3;

    /// <summary>相似度阈值 <c>[0.0, 1.0]</c>，低于该值的分块丢弃；0 表示不过滤。</summary>
    public double ScoreThreshold { get; init; }
}

/// <summary>检索预览请求校验。</summary>
internal sealed class KnowledgeBaseSearchRequestValidator : AbstractValidator<KnowledgeBaseSearchRequest>
{
    /// <summary>查询文本最大长度（字符）。</summary>
    public const int MaxQueryLength = 2000;

    public KnowledgeBaseSearchRequestValidator()
    {
        RuleFor(request => request.Query).NotEmpty().WithMessage("query 不能为空")
            .MaximumLength(MaxQueryLength).WithMessage($"query 不超过 {MaxQueryLength} 字符");
        RuleFor(request => request.TopK).InclusiveBetween(1, 20).WithMessage("topK 取值 [1, 20]");
        RuleFor(request => request.ScoreThreshold).InclusiveBetween(0.0, 1.0).WithMessage("scoreThreshold 取值 [0.0, 1.0]");
    }
}
