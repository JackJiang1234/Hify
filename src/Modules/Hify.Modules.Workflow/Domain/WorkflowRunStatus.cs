namespace Hify.Modules.Workflow.Domain;

/// <summary>工作流执行状态常量（对齐 workflow_run.status 取值）。</summary>
internal static class WorkflowRunStatus
{
    /// <summary>执行中（建 run 时即此态）。</summary>
    public const string Running = "running";

    /// <summary>执行成功（抵达 end 节点）。</summary>
    public const string Succeeded = "succeeded";

    /// <summary>执行失败（节点错误 / 超步 / 超时）。</summary>
    public const string Failed = "failed";
}
