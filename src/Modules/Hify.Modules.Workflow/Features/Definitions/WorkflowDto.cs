namespace Hify.Modules.Workflow.Features.Definitions;

/// <summary>
/// 工作流视图（模块内 DTO；无其它模块依赖 Workflow，故不上提 Contracts）。
/// <see cref="Definition"/> 为画布定义的 JSON 文本（前端 api 层 parse/stringify）。
/// </summary>
internal sealed record WorkflowDto
{
    /// <summary>主键。</summary>
    public long Id { get; init; }

    /// <summary>名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>画布定义 JSON 文本（<c>{nodes,edges}</c>）。</summary>
    public string Definition { get; init; } = "{}";

    /// <summary>状态：<c>draft</c> | <c>published</c>。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>创建时刻（epoch ms）。</summary>
    public long CreatedAt { get; init; }

    /// <summary>最后更新时刻（epoch ms）。</summary>
    public long UpdatedAt { get; init; }
}
