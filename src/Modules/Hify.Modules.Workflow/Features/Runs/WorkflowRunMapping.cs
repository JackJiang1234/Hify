using Hify.Modules.Workflow.Domain;

namespace Hify.Modules.Workflow.Features.Runs;

/// <summary>执行记录实体 → 视图映射。</summary>
internal static class WorkflowRunMapping
{
    public static WorkflowRunDto ToDto(WorkflowRun run) => new()
    {
        Id = run.Id,
        WorkflowId = run.WorkflowId,
        Status = run.Status,
        Inputs = run.Inputs,
        Output = run.Output,
        Trace = run.Trace,
        ErrorMessage = run.ErrorMessage,
        StartedAt = run.StartedAt,
        FinishedAt = run.FinishedAt,
        CreatedAt = run.CreatedAt,
    };
}
