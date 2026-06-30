using Hify.Modules.Workflow.Domain;

namespace Hify.Modules.Workflow.Features.Execution;

/// <summary>
/// 一次工作流执行的内存结果（不含持久化）。引擎产出，由 WorkflowRunService 落库为 workflow_run。
/// </summary>
internal sealed record WorkflowExecution
{
    /// <summary>终态：<c>succeeded</c> | <c>failed</c>（见 <see cref="WorkflowRunStatus"/>）。</summary>
    public string Status { get; init; } = WorkflowRunStatus.Failed;

    /// <summary>最终输出（end 节点的 output 字段）；失败时为空。</summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>逐节点执行轨迹。</summary>
    public IReadOnlyList<NodeTrace> Trace { get; init; } = [];

    /// <summary>失败原因（截断，不含敏感数据）；成功为空。</summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>失败错误码（6xxx）；成功为 0。</summary>
    public int ErrorCode { get; init; }
}

/// <summary>单节点执行轨迹（落入 run.trace jsonb）。</summary>
internal sealed record NodeTrace
{
    /// <summary>节点 Id。</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>节点类型。</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>节点终态：<c>succeeded</c> | <c>failed</c>。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>耗时（毫秒）。</summary>
    public long Ms { get; init; }

    /// <summary>节点输出（键为输出字段名）；condition/失败可能为空。</summary>
    public IReadOnlyDictionary<string, object?> Output { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>失败原因（仅失败节点）。</summary>
    public string Error { get; init; } = string.Empty;
}
