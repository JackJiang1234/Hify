using System.Text.Json;

using Hify.Modules.Workflow.Features.Definitions;
using Hify.Modules.Workflow.Features.Execution.Nodes;

namespace Hify.Modules.Workflow.Tests.Features.Execution.Nodes;

/// <summary>start 节点校验 required + 透出输入的单测。</summary>
public sealed class StartNodeHandlerTests
{
    private const int InvalidRunInputCode = 6003;

    private static readonly StartNodeHandler Handler = new();

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> NoOutputs =
        new Dictionary<string, IReadOnlyDictionary<string, object?>>();

    private static WorkflowNode StartNode(string configJson) => new()
    {
        Id = "s",
        Type = "start",
        Config = JsonDocument.Parse(configJson).RootElement.Clone(),
    };

    private static NodeRunContext Context(WorkflowNode node, IReadOnlyDictionary<string, object?> runInputs) => new()
    {
        Node = node,
        RunInputs = runInputs,
        Outputs = NoOutputs,
    };

    [Fact]
    public async Task Execute_RequiredPresent_OutputsValue()
    {
        var node = StartNode("""{ "inputs": [ { "name": "user_input", "required": true } ] }""");
        var context = Context(node, new Dictionary<string, object?> { ["user_input"] = "Alice" });

        var result = await Handler.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("Alice", result.Data!.Output["user_input"]);
        Assert.Null(result.Data.NextHandle);
    }

    [Fact]
    public async Task Execute_RequiredMissing_FailsWith6003()
    {
        var node = StartNode("""{ "inputs": [ { "name": "user_input", "required": true } ] }""");
        var context = Context(node, new Dictionary<string, object?>());

        var result = await Handler.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(InvalidRunInputCode, result.Code);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Execute_RequiredBlankString_FailsWith6003()
    {
        var node = StartNode("""{ "inputs": [ { "name": "q", "required": true } ] }""");
        var context = Context(node, new Dictionary<string, object?> { ["q"] = "   " });

        var result = await Handler.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(InvalidRunInputCode, result.Code);
    }

    [Fact]
    public async Task Execute_OptionalMissing_DefaultsToEmpty()
    {
        var node = StartNode("""{ "inputs": [ { "name": "opt", "required": false } ] }""");
        var context = Context(node, new Dictionary<string, object?>());

        var result = await Handler.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(string.Empty, result.Data!.Output["opt"]);
    }

    [Fact]
    public async Task Execute_MultipleInputs_AllTransited()
    {
        var node = StartNode("""{ "inputs": [ { "name": "a", "required": true }, { "name": "b", "required": false } ] }""");
        var context = Context(node, new Dictionary<string, object?> { ["a"] = "1", ["b"] = "2" });

        var result = await Handler.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("1", result.Data!.Output["a"]);
        Assert.Equal("2", result.Data.Output["b"]);
    }
}
