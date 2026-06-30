using System.Text.Json;

namespace Hify.Modules.Workflow.Features.Definitions;

/// <summary>
/// 工作流定义（反序列化自 workflow.definition jsonb，<c>{nodes,edges}</c>）。
/// 不可变 record；<see cref="WorkflowNode.Config"/> 为节点类型相关的原始 JSON，按节点类型各自解读。
/// </summary>
internal sealed record WorkflowDefinition
{
    /// <summary>定义版本（前向兼容预留）。</summary>
    public string Version { get; init; } = "1";

    /// <summary>节点列表。</summary>
    public IReadOnlyList<WorkflowNode> Nodes { get; init; } = [];

    /// <summary>连线列表。</summary>
    public IReadOnlyList<WorkflowEdge> Edges { get; init; } = [];
}

/// <summary>工作流节点。<see cref="Position"/> 仅前端渲染用，引擎忽略。</summary>
internal sealed record WorkflowNode
{
    /// <summary>节点 Id（定义内唯一）。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>节点类型（见 <see cref="Domain.WorkflowNodeType"/>）。</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>展示标题。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>节点类型相关配置（原始 JSON，按类型各自解读）。</summary>
    public JsonElement Config { get; init; }
}

/// <summary>工作流连线。<see cref="SourceHandle"/> 用于 condition 多出边（case handle / else）。</summary>
internal sealed record WorkflowEdge
{
    /// <summary>连线 Id。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>源节点 Id。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>目标节点 Id。</summary>
    public string Target { get; init; } = string.Empty;

    /// <summary>源出边句柄（condition 用：case 的 handle 或 <c>else</c>；其余节点为空）。</summary>
    public string SourceHandle { get; init; } = string.Empty;
}
