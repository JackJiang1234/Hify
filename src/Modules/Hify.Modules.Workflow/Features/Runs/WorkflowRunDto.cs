namespace Hify.Modules.Workflow.Features.Runs;

/// <summary>
/// 工作流执行记录视图。<see cref="Inputs"/> / <see cref="Trace"/> 为 JSON 文本（前端 api 层 parse），
/// <see cref="Output"/> 为最终输出文本。
/// </summary>
internal sealed record WorkflowRunDto
{
    /// <summary>主键。</summary>
    public long Id { get; init; }

    /// <summary>所属工作流 Id。</summary>
    public long WorkflowId { get; init; }

    /// <summary>状态：<c>running</c> | <c>succeeded</c> | <c>failed</c>。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>触发输入 JSON 文本。</summary>
    public string Inputs { get; init; } = "{}";

    /// <summary>最终输出文本（纯文本非 JSON）。</summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>逐节点轨迹 JSON 文本（<c>[{nodeId,type,status,ms,output,error}]</c>）。</summary>
    public string Trace { get; init; } = "[]";

    /// <summary>失败原因（成功为空）。</summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>开始时刻（epoch ms）。</summary>
    public long StartedAt { get; init; }

    /// <summary>结束时刻（epoch ms）。</summary>
    public long FinishedAt { get; init; }

    /// <summary>创建时刻（epoch ms）。</summary>
    public long CreatedAt { get; init; }
}
