using FluentValidation;

namespace Hify.Modules.Workflow.Features.Definitions;

/// <summary>新建工作流请求。<see cref="Definition"/> 为画布定义 JSON 文本（保存仅校验 JSON 合法，发布才校验图）。</summary>
internal sealed record CreateWorkflowRequest
{
    /// <summary>名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>画布定义 JSON 文本。</summary>
    public string Definition { get; init; } = "{}";
}

/// <summary>更新工作流请求。</summary>
internal sealed record UpdateWorkflowRequest
{
    /// <summary>名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>画布定义 JSON 文本。</summary>
    public string Definition { get; init; } = "{}";
}

/// <summary>工作流请求的共用上下界。</summary>
internal static class WorkflowValidation
{
    /// <summary>名称最大长度。</summary>
    public const int MaxNameLength = 128;

    /// <summary>描述最大长度。</summary>
    public const int MaxDescriptionLength = 512;
}

/// <summary>新建请求校验。</summary>
internal sealed class CreateWorkflowRequestValidator : AbstractValidator<CreateWorkflowRequest>
{
    public CreateWorkflowRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("name 不能为空")
            .MaximumLength(WorkflowValidation.MaxNameLength).WithMessage($"name 不超过 {WorkflowValidation.MaxNameLength} 字符");
        RuleFor(request => request.Description)
            .MaximumLength(WorkflowValidation.MaxDescriptionLength).WithMessage($"description 不超过 {WorkflowValidation.MaxDescriptionLength} 字符");
    }
}

/// <summary>更新请求校验。</summary>
internal sealed class UpdateWorkflowRequestValidator : AbstractValidator<UpdateWorkflowRequest>
{
    public UpdateWorkflowRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("name 不能为空")
            .MaximumLength(WorkflowValidation.MaxNameLength).WithMessage($"name 不超过 {WorkflowValidation.MaxNameLength} 字符");
        RuleFor(request => request.Description)
            .MaximumLength(WorkflowValidation.MaxDescriptionLength).WithMessage($"description 不超过 {WorkflowValidation.MaxDescriptionLength} 字符");
    }
}
