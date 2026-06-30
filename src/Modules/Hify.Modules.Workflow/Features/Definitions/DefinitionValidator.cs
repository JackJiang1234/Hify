using System.Text.Json;

using Hify.Modules.Workflow.Domain;
using Hify.Modules.Workflow.Features.Execution;
using Hify.Shared.Results;

namespace Hify.Modules.Workflow.Features.Definitions;

/// <summary>
/// 工作流定义校验器：解析 definition JSON 并校验图合法性（线性 + 单层分支约束，见设计 §6）。
/// 解析与校验合一——成功回传解析好的 <see cref="WorkflowDefinition"/>，失败以 6002 返回首个问题。
/// 纯函数、无 I/O，可独立单测。
/// </summary>
internal sealed class DefinitionValidator
{
    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>解析并校验定义。</summary>
    /// <param name="definitionJson">workflow.definition 的 jsonb 文本（<c>{nodes,edges}</c>）。</param>
    public Result<WorkflowDefinition> Validate(string definitionJson)
    {
        WorkflowDefinition? definition;
        try
        {
            definition = string.IsNullOrWhiteSpace(definitionJson)
                ? null
                : JsonSerializer.Deserialize<WorkflowDefinition>(definitionJson, ParseOptions);
        }
        catch (JsonException ex)
        {
            return Fail($"定义 JSON 解析失败：{ex.Message}");
        }

        if (definition is null)
        {
            return Fail("定义为空。");
        }

        var nodes = definition.Nodes;
        var edges = definition.Edges;

        // 1. 节点 Id 非空且唯一。
        var byId = new Dictionary<string, WorkflowNode>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                return Fail("存在 Id 为空的节点。");
            }

            if (!byId.TryAdd(node.Id, node))
            {
                return Fail($"节点 Id 重复：{node.Id}。");
            }
        }

        // 2. 节点类型合法。
        foreach (var node in nodes)
        {
            if (!WorkflowNodeType.All.Contains(node.Type))
            {
                return Fail($"节点 {node.Id} 的类型非法：{node.Type}。");
            }
        }

        // 3. 有且仅一个 start；至少一个 end。
        var startCount = nodes.Count(n => n.Type == WorkflowNodeType.Start);
        if (startCount != 1)
        {
            return Fail($"必须有且仅有一个 start 节点（当前 {startCount} 个）。");
        }

        if (!nodes.Any(n => n.Type == WorkflowNodeType.End))
        {
            return Fail("至少需要一个 end 节点。");
        }

        // 4. 连线两端必须引用存在的节点。
        foreach (var edge in edges)
        {
            if (!byId.ContainsKey(edge.Source))
            {
                return Fail($"连线 {edge.Id} 的源节点不存在：{edge.Source}。");
            }

            if (!byId.ContainsKey(edge.Target))
            {
                return Fail($"连线 {edge.Id} 的目标节点不存在：{edge.Target}。");
            }
        }

        var outgoing = nodes.ToDictionary(n => n.Id, _ => new List<WorkflowEdge>(), StringComparer.Ordinal);
        var inDegree = nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            outgoing[edge.Source].Add(edge);
            inDegree[edge.Target]++;
        }

        // 5. 入度：start 必须为 0；其余至多 1（不支持汇合）。
        foreach (var node in nodes)
        {
            if (node.Type == WorkflowNodeType.Start)
            {
                if (inDegree[node.Id] != 0)
                {
                    return Fail("start 节点不允许有入边。");
                }
            }
            else if (inDegree[node.Id] > 1)
            {
                return Fail($"节点 {node.Id} 有多条入边（不支持汇合，仅线性 + 单层分支）。");
            }
        }

        // 6. 出度：end 必须为 0；condition ≥1 且每条出边带 handle；其余至多 1。
        foreach (var node in nodes)
        {
            var outs = outgoing[node.Id];
            switch (node.Type)
            {
                case WorkflowNodeType.End when outs.Count != 0:
                    return Fail("end 节点不允许有出边。");

                case WorkflowNodeType.Condition when outs.Count == 0:
                    return Fail($"condition 节点 {node.Id} 至少需要一条出边。");

                case WorkflowNodeType.Condition when outs.Any(e => string.IsNullOrWhiteSpace(e.SourceHandle)):
                    return Fail($"condition 节点 {node.Id} 的出边必须带 handle（case 或 else）。");

                case WorkflowNodeType.Start or WorkflowNodeType.Llm or WorkflowNodeType.Tool when outs.Count > 1:
                    return Fail($"节点 {node.Id} 有多条出边（仅 condition 可分支）。");

                default:
                    break;
            }
        }

        // 7. 无环。
        var cycleNode = FindCycleNode(nodes, outgoing);
        if (cycleNode is not null)
        {
            return Fail($"定义存在环（涉及节点 {cycleNode}）。");
        }

        // 8. 变量引用：{{nodeId.field}} 的 nodeId 必须存在且为引用节点的祖先（前驱链上）。
        var ancestors = ComputeAncestors(nodes, edges);
        foreach (var node in nodes)
        {
            var configText = node.Config.ValueKind == JsonValueKind.Undefined
                ? string.Empty
                : node.Config.GetRawText();

            foreach (var reference in VariableRef.Extract(configText))
            {
                if (!byId.ContainsKey(reference.NodeId))
                {
                    return Fail($"节点 {node.Id} 引用了不存在的节点：{reference.NodeId}。");
                }

                if (reference.NodeId == node.Id || !ancestors[node.Id].Contains(reference.NodeId))
                {
                    return Fail($"节点 {node.Id} 引用的 {reference.NodeId} 不在其前驱链上。");
                }
            }
        }

        return Result<WorkflowDefinition>.Ok(definition);
    }

    private static Result<WorkflowDefinition> Fail(string message) =>
        Result<WorkflowDefinition>.Fail((int)WorkflowErrorCode.InvalidDefinition, message);

    // 三色 DFS 判环：返回环上任一节点 Id，无环返回 null。
    private static string? FindCycleNode(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyDictionary<string, List<WorkflowEdge>> outgoing)
    {
        // 0=未访问 1=在栈中 2=已完成。
        var color = nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
        string? cycle = null;

        bool Visit(string id)
        {
            color[id] = 1;
            foreach (var edge in outgoing[id])
            {
                if (color[edge.Target] == 1)
                {
                    cycle = edge.Target;
                    return true;
                }

                if (color[edge.Target] == 0 && Visit(edge.Target))
                {
                    return true;
                }
            }

            color[id] = 2;
            return false;
        }

        foreach (var node in nodes)
        {
            if (color[node.Id] == 0 && Visit(node.Id))
            {
                return cycle;
            }
        }

        return null;
    }

    // 每个节点的祖先集合（能到达它的全部节点）。前置：已确认无环。
    private static Dictionary<string, HashSet<string>> ComputeAncestors(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowEdge> edges)
    {
        var reverse = nodes.ToDictionary(n => n.Id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            reverse[edge.Target].Add(edge.Source);
        }

        var ancestors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>(reverse[node.Id]);
            while (stack.Count > 0)
            {
                var parent = stack.Pop();
                if (set.Add(parent))
                {
                    foreach (var grandParent in reverse[parent])
                    {
                        stack.Push(grandParent);
                    }
                }
            }

            ancestors[node.Id] = set;
        }

        return ancestors;
    }
}
