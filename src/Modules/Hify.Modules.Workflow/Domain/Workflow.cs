using Hify.Shared.Persistence;

namespace Hify.Modules.Workflow.Domain;

/// <summary>
/// 工作流实体（一份可执行的流程定义）。定义以单 jsonb 整存于 <see cref="Definition"/>（<c>{nodes,edges}</c>），
/// 前端 Vue Flow 拖拽产出、引擎按图遍历执行。状态见 <see cref="WorkflowStatus"/>，发布前跑图校验。
/// </summary>
internal sealed class Workflow : EntityBase
{
    /// <summary>名称（同未删行内唯一）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>画布定义（jsonb 文本，<c>{nodes,edges}</c>）；空工作流为 <c>{}</c>。</summary>
    public string Definition { get; set; } = "{}";

    /// <summary>状态：<c>draft</c> | <c>published</c>（见 <see cref="WorkflowStatus"/>）。</summary>
    public string Status { get; set; } = WorkflowStatus.Draft;
}
