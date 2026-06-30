using System.Globalization;

using Hify.Modules.Workflow.Domain;
using Hify.Shared.Results;

namespace Hify.Modules.Workflow.Features.Execution.Nodes;

/// <summary>
/// condition 节点：按 cases 顺序求值单比较（<c>left op right</c>），首个为真选其 handle，全假走 <c>else</c>。
/// 比较前两侧均做变量解析；gt/lt 优先按数值（失败回退字符串），eq/ne/contains 按字符串（序数）。
/// </summary>
internal sealed class ConditionNodeHandler : INodeHandler
{
    /// <summary>默认兜底出边句柄。</summary>
    public const string ElseHandle = "else";

    private readonly VariableResolver _resolver;

    /// <summary>构造。</summary>
    /// <param name="resolver">变量解析器。</param>
    public ConditionNodeHandler(VariableResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
    }

    /// <inheritdoc />
    public string NodeType => WorkflowNodeType.Condition;

    /// <inheritdoc />
    public Task<Result<NodeResult>> ExecuteAsync(NodeRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var config = NodeConfigJson.Read<ConditionConfig>(context.Node.Config);

        foreach (var branch in config.Cases)
        {
            var left = _resolver.ResolveString(branch.Left, context.Outputs);
            var right = _resolver.ResolveString(branch.Right, context.Outputs);
            if (Evaluate(left, branch.Op, right))
            {
                var handle = string.IsNullOrWhiteSpace(branch.Handle) ? ElseHandle : branch.Handle;
                return Task.FromResult(Result<NodeResult>.Ok(NodeResult.Branch(handle)));
            }
        }

        return Task.FromResult(Result<NodeResult>.Ok(NodeResult.Branch(ElseHandle)));
    }

    private static bool Evaluate(string left, string op, string right)
    {
        return op switch
        {
            ConditionOp.Eq => string.Equals(left, right, StringComparison.Ordinal),
            ConditionOp.Ne => !string.Equals(left, right, StringComparison.Ordinal),
            ConditionOp.Contains => left.Contains(right, StringComparison.Ordinal),
            ConditionOp.Gt => Compare(left, right) > 0,
            ConditionOp.Lt => Compare(left, right) < 0,
            _ => false,
        };
    }

    // gt/lt 比较：两侧都能解析为数值则按数值，否则按序数字符串。
    private static int Compare(string left, string right)
    {
        if (double.TryParse(left, NumberStyles.Any, CultureInfo.InvariantCulture, out var leftNumber)
            && double.TryParse(right, NumberStyles.Any, CultureInfo.InvariantCulture, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.CompareOrdinal(left, right);
    }

    private static class ConditionOp
    {
        public const string Eq = "eq";
        public const string Ne = "ne";
        public const string Contains = "contains";
        public const string Gt = "gt";
        public const string Lt = "lt";
    }

    private sealed record ConditionConfig
    {
        public IReadOnlyList<ConditionCase> Cases { get; init; } = [];
    }

    private sealed record ConditionCase
    {
        public string Handle { get; init; } = string.Empty;

        public string Left { get; init; } = string.Empty;

        public string Op { get; init; } = string.Empty;

        public string Right { get; init; } = string.Empty;
    }
}
