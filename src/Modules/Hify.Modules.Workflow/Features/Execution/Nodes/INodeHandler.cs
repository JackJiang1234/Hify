using Hify.Modules.Workflow.Features.Definitions;
using Hify.Shared.Results;

using NodeOutputs = System.Collections.Generic.IReadOnlyDictionary<
    string,
    System.Collections.Generic.IReadOnlyDictionary<string, object?>>;

namespace Hify.Modules.Workflow.Features.Execution.Nodes;

/// <summary>
/// 节点执行器：执行单个节点，返回输出 + 下一跳决策。每种节点类型一个实现，按 <see cref="NodeType"/> 注册。
/// 可预期失败（输入缺失、上游 LLM/工具错误）以失败 <see cref="Result{T}"/>（6xxx）返回，不抛异常。
/// </summary>
internal interface INodeHandler
{
    /// <summary>处理的节点类型（对齐 <see cref="Domain.WorkflowNodeType"/>）。</summary>
    string NodeType { get; }

    /// <summary>执行节点。</summary>
    /// <param name="context">节点运行上下文（节点定义 + 已有输出 + 运行输入 + 解析器）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<NodeResult>> ExecuteAsync(NodeRunContext context, CancellationToken cancellationToken);
}

/// <summary>节点执行结果：写回上下文的输出 + 出边选择（仅 condition 用 <see cref="NextHandle"/>）。</summary>
internal sealed record NodeResult
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyOutput =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>本节点输出（键即输出字段名）。无输出节点为空字典。</summary>
    public IReadOnlyDictionary<string, object?> Output { get; init; } = EmptyOutput;

    /// <summary>选中的出边句柄（condition 用：case handle 或 <c>else</c>）；其余节点为 <c>null</c>（走唯一出边）。</summary>
    public string? NextHandle { get; init; }

    /// <summary>无输出、无分支（如 end 之前的占位）。</summary>
    public static NodeResult None { get; } = new();

    /// <summary>构造带输出的结果。</summary>
    /// <param name="output">输出字段。</param>
    public static NodeResult FromOutput(IReadOnlyDictionary<string, object?> output) => new() { Output = output };

    /// <summary>构造单字段输出。</summary>
    /// <param name="field">字段名。</param>
    /// <param name="value">字段值。</param>
    public static NodeResult Single(string field, object? value) =>
        new() { Output = new Dictionary<string, object?>(StringComparer.Ordinal) { [field] = value } };

    /// <summary>构造分支选择结果（无输出）。</summary>
    /// <param name="handle">选中的出边句柄。</param>
    public static NodeResult Branch(string handle) => new() { NextHandle = handle };
}

/// <summary>节点运行上下文。</summary>
internal sealed class NodeRunContext
{
    /// <summary>当前执行的节点。</summary>
    public required WorkflowNode Node { get; init; }

    /// <summary>已执行节点的输出（nodeId -&gt; 字段 -&gt; 值），供变量解析。</summary>
    public required NodeOutputs Outputs { get; init; }

    /// <summary>工作流触发输入（供 start 节点校验/透出）。</summary>
    public required IReadOnlyDictionary<string, object?> RunInputs { get; init; }
}
