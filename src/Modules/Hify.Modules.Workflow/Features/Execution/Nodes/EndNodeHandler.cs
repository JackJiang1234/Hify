using Hify.Modules.Workflow.Domain;
using Hify.Shared.Results;

namespace Hify.Modules.Workflow.Features.Execution.Nodes;

/// <summary>
/// end 节点：解析 <c>config.output</c> 模板为最终输出文本，写入字段 <c>output</c>，由引擎汇总为 run.output。
/// </summary>
internal sealed class EndNodeHandler : INodeHandler
{
    private readonly VariableResolver _resolver;

    /// <summary>构造。</summary>
    /// <param name="resolver">变量解析器。</param>
    public EndNodeHandler(VariableResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
    }

    /// <inheritdoc />
    public string NodeType => WorkflowNodeType.End;

    /// <inheritdoc />
    public Task<Result<NodeResult>> ExecuteAsync(NodeRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var config = NodeConfigJson.Read<EndConfig>(context.Node.Config);
        var output = _resolver.ResolveString(config.Output, context.Outputs);

        return Task.FromResult(Result<NodeResult>.Ok(NodeResult.Single(NodeOutputField.Output, output)));
    }

    private sealed record EndConfig
    {
        public string Output { get; init; } = string.Empty;
    }
}
