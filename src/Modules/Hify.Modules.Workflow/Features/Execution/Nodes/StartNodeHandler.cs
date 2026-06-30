using Hify.Modules.Workflow.Domain;
using Hify.Shared.Results;

namespace Hify.Modules.Workflow.Features.Execution.Nodes;

/// <summary>
/// start 节点：按声明的 inputs 校验 required，并把运行输入透出为本节点输出（字段即各 input 名）。
/// 后续节点以 <c>{{startId.inputName}}</c> 引用。
/// </summary>
internal sealed class StartNodeHandler : INodeHandler
{
    /// <inheritdoc />
    public string NodeType => WorkflowNodeType.Start;

    /// <inheritdoc />
    public Task<Result<NodeResult>> ExecuteAsync(NodeRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var config = NodeConfigJson.Read<StartConfig>(context.Node.Config);
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var input in config.Inputs)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
            {
                continue;
            }

            var present = context.RunInputs.TryGetValue(input.Name, out var value) && !IsBlank(value);
            if (input.Required && !present)
            {
                return Task.FromResult(Result<NodeResult>.Fail(
                    (int)WorkflowErrorCode.InvalidRunInput,
                    $"缺少必填输入：{input.Name}。"));
            }

            output[input.Name] = present ? value : string.Empty;
        }

        return Task.FromResult(Result<NodeResult>.Ok(NodeResult.FromOutput(output)));
    }

    private static bool IsBlank(object? value) =>
        value is null || (value is string s && string.IsNullOrWhiteSpace(s));

    private sealed record StartConfig
    {
        public IReadOnlyList<StartInput> Inputs { get; init; } = [];
    }

    private sealed record StartInput
    {
        public string Name { get; init; } = string.Empty;

        public string Type { get; init; } = "string";

        public bool Required { get; init; }
    }
}
