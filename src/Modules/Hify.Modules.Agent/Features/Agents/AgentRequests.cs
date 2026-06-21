using FluentValidation;

using Hify.Contracts.Agent;

namespace Hify.Modules.Agent.Features.Agents;

/// <summary>创建 Agent 请求。引用 ID（模型/工具/知识库）的存在性在服务层校验（方案 B）。</summary>
internal sealed record CreateAgentRequest
{
    /// <summary>名称（唯一）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>引用的 chat 模型 Id。</summary>
    public long ModelId { get; init; }

    /// <summary>系统提示词。</summary>
    public string SystemPrompt { get; init; } = string.Empty;

    /// <summary>模型生成参数（可空，留空用模型默认）。</summary>
    public ModelParams? ModelParams { get; init; }

    /// <summary>RAG 检索参数。</summary>
    public RetrievalParams RetrievalParams { get; init; } = new();

    /// <summary>工具调用循环上限。</summary>
    public int MaxIterations { get; init; } = 5;

    /// <summary>绑定的 MCP 工具 Id 列表。</summary>
    public IReadOnlyList<long> ToolIds { get; init; } = [];

    /// <summary>绑定的知识库 Id 列表。</summary>
    public IReadOnlyList<long> KnowledgeBaseIds { get; init; } = [];

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>更新 Agent 请求。绑定列表为期望的完整集合，服务层与现存绑定做差量替换。</summary>
internal sealed record UpdateAgentRequest
{
    /// <summary>名称（唯一）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>引用的 chat 模型 Id。</summary>
    public long ModelId { get; init; }

    /// <summary>系统提示词。</summary>
    public string SystemPrompt { get; init; } = string.Empty;

    /// <summary>模型生成参数（可空，留空用模型默认）。</summary>
    public ModelParams? ModelParams { get; init; }

    /// <summary>RAG 检索参数。</summary>
    public RetrievalParams RetrievalParams { get; init; } = new();

    /// <summary>工具调用循环上限。</summary>
    public int MaxIterations { get; init; } = 5;

    /// <summary>绑定的 MCP 工具 Id 列表（完整集合）。</summary>
    public IReadOnlyList<long> ToolIds { get; init; } = [];

    /// <summary>绑定的知识库 Id 列表（完整集合）。</summary>
    public IReadOnlyList<long> KnowledgeBaseIds { get; init; } = [];

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>Agent 请求的共用校验上下界与谓词。</summary>
internal static class AgentValidation
{
    /// <summary>系统提示词最大长度（字符），防止超出模型上下文窗口。</summary>
    public const int MaxSystemPromptLength = 8000;

    /// <summary>工具/知识库绑定数量上限。</summary>
    public const int MaxBindings = 50;

    public static bool BePositive(long id) => id > 0;

    public static bool AllPositive(IReadOnlyList<long> ids) => ids.All(id => id > 0);

    public static bool AllDistinct(IReadOnlyList<long> ids) => ids.Distinct().Count() == ids.Count;
}

/// <summary>模型生成参数校验（仅当请求提供时执行）。</summary>
internal sealed class ModelParamsValidator : AbstractValidator<ModelParams>
{
    public ModelParamsValidator()
    {
        RuleFor(p => p.Temperature).InclusiveBetween(0.0, 2.0).When(p => p.Temperature.HasValue).WithMessage("temperature 取值 [0.0, 2.0]");
        RuleFor(p => p.TopP).InclusiveBetween(0.0, 1.0).When(p => p.TopP.HasValue).WithMessage("topP 取值 [0.0, 1.0]");
        RuleFor(p => p.MaxTokens).GreaterThan(0).When(p => p.MaxTokens.HasValue).WithMessage("maxTokens 须大于 0");
    }
}

/// <summary>RAG 检索参数校验。</summary>
internal sealed class RetrievalParamsValidator : AbstractValidator<RetrievalParams>
{
    public RetrievalParamsValidator()
    {
        RuleFor(p => p.TopK).InclusiveBetween(1, 20).WithMessage("topK 取值 [1, 20]");
        RuleFor(p => p.ScoreThreshold).InclusiveBetween(0.0, 1.0).WithMessage("scoreThreshold 取值 [0.0, 1.0]");
    }
}

/// <summary>创建请求校验。</summary>
internal sealed class CreateAgentRequestValidator : AbstractValidator<CreateAgentRequest>
{
    public CreateAgentRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage("name 不能为空").MaximumLength(128).WithMessage("name 不超过 128 字符");
        RuleFor(request => request.Description).MaximumLength(512).WithMessage("description 不超过 512 字符");
        RuleFor(request => request.ModelId).Must(AgentValidation.BePositive).WithMessage("modelId 非法");
        RuleFor(request => request.SystemPrompt).MaximumLength(AgentValidation.MaxSystemPromptLength).WithMessage($"systemPrompt 不超过 {AgentValidation.MaxSystemPromptLength} 字符");
        RuleFor(request => request.MaxIterations).InclusiveBetween(1, 20).WithMessage("maxIterations 取值 [1, 20]");
        RuleFor(request => request.ModelParams!).SetValidator(new ModelParamsValidator()).When(request => request.ModelParams is not null);
        RuleFor(request => request.RetrievalParams).NotNull().SetValidator(new RetrievalParamsValidator());
        RuleFor(request => request.ToolIds).Must(AgentValidation.AllPositive).WithMessage("toolIds 含非法 Id").Must(AgentValidation.AllDistinct).WithMessage("toolIds 含重复").Must(ids => ids.Count <= AgentValidation.MaxBindings).WithMessage($"toolIds 至多 {AgentValidation.MaxBindings} 个");
        RuleFor(request => request.KnowledgeBaseIds).Must(AgentValidation.AllPositive).WithMessage("knowledgeBaseIds 含非法 Id").Must(AgentValidation.AllDistinct).WithMessage("knowledgeBaseIds 含重复").Must(ids => ids.Count <= AgentValidation.MaxBindings).WithMessage($"knowledgeBaseIds 至多 {AgentValidation.MaxBindings} 个");
    }
}

/// <summary>更新请求校验。</summary>
internal sealed class UpdateAgentRequestValidator : AbstractValidator<UpdateAgentRequest>
{
    public UpdateAgentRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage("name 不能为空").MaximumLength(128).WithMessage("name 不超过 128 字符");
        RuleFor(request => request.Description).MaximumLength(512).WithMessage("description 不超过 512 字符");
        RuleFor(request => request.ModelId).Must(AgentValidation.BePositive).WithMessage("modelId 非法");
        RuleFor(request => request.SystemPrompt).MaximumLength(AgentValidation.MaxSystemPromptLength).WithMessage($"systemPrompt 不超过 {AgentValidation.MaxSystemPromptLength} 字符");
        RuleFor(request => request.MaxIterations).InclusiveBetween(1, 20).WithMessage("maxIterations 取值 [1, 20]");
        RuleFor(request => request.ModelParams!).SetValidator(new ModelParamsValidator()).When(request => request.ModelParams is not null);
        RuleFor(request => request.RetrievalParams).NotNull().SetValidator(new RetrievalParamsValidator());
        RuleFor(request => request.ToolIds).Must(AgentValidation.AllPositive).WithMessage("toolIds 含非法 Id").Must(AgentValidation.AllDistinct).WithMessage("toolIds 含重复").Must(ids => ids.Count <= AgentValidation.MaxBindings).WithMessage($"toolIds 至多 {AgentValidation.MaxBindings} 个");
        RuleFor(request => request.KnowledgeBaseIds).Must(AgentValidation.AllPositive).WithMessage("knowledgeBaseIds 含非法 Id").Must(AgentValidation.AllDistinct).WithMessage("knowledgeBaseIds 含重复").Must(ids => ids.Count <= AgentValidation.MaxBindings).WithMessage($"knowledgeBaseIds 至多 {AgentValidation.MaxBindings} 个");
    }
}
