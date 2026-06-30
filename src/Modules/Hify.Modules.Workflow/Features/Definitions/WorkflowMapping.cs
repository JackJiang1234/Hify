namespace Hify.Modules.Workflow.Features.Definitions;

/// <summary>工作流实体 → 视图映射。</summary>
internal static class WorkflowMapping
{
    public static WorkflowDto ToDto(Domain.Workflow workflow) => new()
    {
        Id = workflow.Id,
        Name = workflow.Name,
        Description = workflow.Description,
        Definition = workflow.Definition,
        Status = workflow.Status,
        CreatedAt = workflow.CreatedAt,
        UpdatedAt = workflow.UpdatedAt,
    };
}
