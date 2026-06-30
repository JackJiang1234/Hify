using Hify.Shared.Persistence;

namespace Hify.Modules.Workflow.Domain;

/// <summary>
/// 工作流执行记录（一期同步执行）。仅持有 <see cref="WorkflowId"/> 引用，引用完整性由应用层维护。
/// 逐节点轨迹内联 <see cref="Trace"/>（jsonb），供调试与前端结果展示，不另建 node_run 表。
/// </summary>
internal sealed class WorkflowRun : EntityBase
{
    /// <summary>所属工作流 Id（-&gt; workflow.workflow）。</summary>
    public long WorkflowId { get; set; }

    /// <summary>状态：<c>running</c> | <c>succeeded</c> | <c>failed</c>（见 <see cref="WorkflowRunStatus"/>）。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>触发输入（jsonb 文本，满足 start 节点声明的 inputs）。</summary>
    public string Inputs { get; set; } = "{}";

    /// <summary>最终输出文本（end 节点产出，纯文本非 JSON）。</summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>逐节点执行轨迹（jsonb 文本，<c>[{nodeId,status,ms,input,output}]</c>）。</summary>
    public string Trace { get; set; } = "[]";

    /// <summary>失败原因（截断、不含凭证/PII）。</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>开始时刻（epoch ms）。</summary>
    public long StartedAt { get; set; }

    /// <summary>结束时刻（epoch ms）。</summary>
    public long FinishedAt { get; set; }
}
