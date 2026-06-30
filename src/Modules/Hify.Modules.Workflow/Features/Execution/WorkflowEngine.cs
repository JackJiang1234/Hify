using System.Diagnostics;

using Hify.Modules.Workflow.Domain;
using Hify.Modules.Workflow.Features.Definitions;
using Hify.Modules.Workflow.Features.Execution.Nodes;

namespace Hify.Modules.Workflow.Features.Execution;

/// <summary>
/// 工作流执行引擎：按图遍历（线性 + 单层条件分支），逐节点经 <see cref="INodeHandler"/> 执行，
/// 维护变量池与轨迹，产出 <see cref="WorkflowExecution"/>（不落库）。同步执行（一期），
/// 防失控以 <see cref="MaxSteps"/> 限步；节点失败 / 取消 / 图断裂均以 failed 终态返回，不抛业务异常。
/// </summary>
internal sealed class WorkflowEngine
{
    private const int MaxSteps = 64;

    private readonly IReadOnlyDictionary<string, INodeHandler> _handlers;

    /// <summary>构造。</summary>
    /// <param name="handlers">各节点类型执行器（按 <see cref="INodeHandler.NodeType"/> 分发）。</param>
    public WorkflowEngine(IEnumerable<INodeHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _handlers = handlers.ToDictionary(handler => handler.NodeType, StringComparer.Ordinal);
    }

    /// <summary>执行工作流定义。前置：定义已通过 <see cref="DefinitionValidator"/> 校验。</summary>
    /// <param name="definition">工作流定义。</param>
    /// <param name="inputs">触发输入。</param>
    /// <param name="cancellationToken">取消令牌（同步总超时经此传入）。</param>
    public async Task<WorkflowExecution> ExecuteAsync(
        WorkflowDefinition definition,
        IReadOnlyDictionary<string, object?> inputs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(inputs);

        var byId = definition.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var outgoing = definition.Nodes.ToDictionary(node => node.Id, _ => new List<WorkflowEdge>(), StringComparer.Ordinal);
        foreach (var edge in definition.Edges)
        {
            if (outgoing.TryGetValue(edge.Source, out var list))
            {
                list.Add(edge);
            }
        }

        var start = definition.Nodes.FirstOrDefault(node => node.Type == WorkflowNodeType.Start);
        if (start is null)
        {
            return Failed(WorkflowErrorCode.InvalidDefinition, "定义缺少 start 节点。", []);
        }

        var state = new ExecutionState(inputs);
        var current = start;

        for (var step = 0; ; step++)
        {
            if (step >= MaxSteps)
            {
                return Failed(WorkflowErrorCode.MaxStepsExceeded, $"执行超过最大步数 {MaxSteps}（疑似环或失控）。", state.Trace);
            }

            if (!_handlers.TryGetValue(current.Type, out var handler))
            {
                return Failed(WorkflowErrorCode.InvalidDefinition, $"无节点 {current.Type} 的执行器。", state.Trace);
            }

            var context = new NodeRunContext
            {
                Node = current,
                Outputs = state.Outputs,
                RunInputs = state.RunInputs,
            };

            var startedAt = Stopwatch.GetTimestamp();
            Shared.Results.Result<NodeResult> result;
            try
            {
                result = await handler.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                state.AddTrace(FailTrace(current, startedAt, "已取消 / 超时。"));
                return Failed(WorkflowErrorCode.ExecutionTimeout, "执行已取消或超时。", state.Trace);
            }

            var elapsedMs = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            if (result.Code != 200 || result.Data is null)
            {
                state.AddTrace(new NodeTrace
                {
                    NodeId = current.Id,
                    Type = current.Type,
                    Status = WorkflowRunStatus.Failed,
                    Ms = elapsedMs,
                    Error = result.Message,
                });
                return Failed((WorkflowErrorCode)result.Code, result.Message, state.Trace);
            }

            state.SetOutput(current.Id, result.Data.Output);
            state.AddTrace(new NodeTrace
            {
                NodeId = current.Id,
                Type = current.Type,
                Status = WorkflowRunStatus.Succeeded,
                Ms = elapsedMs,
                Output = result.Data.Output,
            });

            if (current.Type == WorkflowNodeType.End)
            {
                var output = result.Data.Output.TryGetValue(NodeOutputField.Output, out var value)
                    ? value?.ToString() ?? string.Empty
                    : string.Empty;
                return new WorkflowExecution
                {
                    Status = WorkflowRunStatus.Succeeded,
                    Output = output,
                    Trace = state.Trace,
                };
            }

            var next = SelectNext(current, result.Data, outgoing, byId);
            if (next is null)
            {
                return Failed(
                    WorkflowErrorCode.NodeExecutionFailed,
                    $"节点 {current.Id} 找不到可走的出边（图断裂或 condition 无匹配分支）。",
                    state.Trace);
            }

            current = next;
        }
    }

    private static WorkflowNode? SelectNext(
        WorkflowNode current,
        NodeResult result,
        IReadOnlyDictionary<string, List<WorkflowEdge>> outgoing,
        IReadOnlyDictionary<string, WorkflowNode> byId)
    {
        var edges = outgoing[current.Id];

        WorkflowEdge? chosen;
        if (current.Type == WorkflowNodeType.Condition)
        {
            var handle = result.NextHandle ?? ConditionNodeHandler.ElseHandle;
            chosen = edges.FirstOrDefault(edge => string.Equals(edge.SourceHandle, handle, StringComparison.Ordinal));
        }
        else
        {
            chosen = edges.Count > 0 ? edges[0] : null;
        }

        return chosen is not null && byId.TryGetValue(chosen.Target, out var target) ? target : null;
    }

    private static WorkflowExecution Failed(WorkflowErrorCode code, string message, IReadOnlyList<NodeTrace> trace) =>
        new()
        {
            Status = WorkflowRunStatus.Failed,
            ErrorMessage = message,
            ErrorCode = (int)code,
            Trace = trace,
        };

    private static NodeTrace FailTrace(WorkflowNode node, long startedAt, string error) => new()
    {
        NodeId = node.Id,
        Type = node.Type,
        Status = WorkflowRunStatus.Failed,
        Ms = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
        Error = error,
    };
}
