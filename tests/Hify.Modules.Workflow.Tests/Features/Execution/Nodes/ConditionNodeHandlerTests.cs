using System.Text.Json;

using Hify.Modules.Workflow.Features.Definitions;
using Hify.Modules.Workflow.Features.Execution;
using Hify.Modules.Workflow.Features.Execution.Nodes;

namespace Hify.Modules.Workflow.Tests.Features.Execution.Nodes;

/// <summary>condition 节点单比较求值的表驱动单测（覆盖 eq/ne/contains/gt/lt × 命中/未命中 + else 兜底）。</summary>
public sealed class ConditionNodeHandlerTests
{
    private static readonly ConditionNodeHandler Handler = new(new VariableResolver());

    private static readonly IReadOnlyDictionary<string, object?> NoInputs = new Dictionary<string, object?>();

    // 单 case：left 取 {{s.v}}，命中走 "hit"，否则 else。
    private static NodeRunContext Context(string leftValue, string op, string right)
    {
        // 用拼接而非插值，保留字面量 {{s.v}}（变量引用语法）。
        var config = "{ \"cases\": [ { \"handle\": \"hit\", \"left\": \"{{s.v}}\", \"op\": \""
            + op + "\", \"right\": \"" + right + "\" } ] }";
        var node = new WorkflowNode
        {
            Id = "c",
            Type = "condition",
            Config = JsonDocument.Parse(config).RootElement.Clone(),
        };

        var outputs = new Dictionary<string, IReadOnlyDictionary<string, object?>>
        {
            ["s"] = new Dictionary<string, object?> { ["v"] = leftValue },
        };

        return new NodeRunContext
        {
            Node = node,
            RunInputs = NoInputs,
            Outputs = outputs,
        };
    }

    [Theory]
    [InlineData("hello", "eq", "hello", "hit")]
    [InlineData("hello", "eq", "bye", "else")]
    [InlineData("hello", "ne", "bye", "hit")]
    [InlineData("hello", "ne", "hello", "else")]
    [InlineData("hello", "contains", "ell", "hit")]
    [InlineData("hello", "contains", "xyz", "else")]
    [InlineData("5", "gt", "3", "hit")]
    [InlineData("5", "lt", "3", "else")]
    [InlineData("3", "lt", "5", "hit")]
    [InlineData("10", "gt", "9", "hit")] // 数值比较：而非字符串（"10" < "9"）
    [InlineData("abc", "gt", "abd", "else")] // 非数值回退字符串序数
    public async Task Execute_SingleCase_SelectsExpectedHandle(string left, string op, string right, string expected)
    {
        var result = await Handler.ExecuteAsync(Context(left, op, right), CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(expected, result.Data!.NextHandle);
    }

    [Fact]
    public async Task Execute_NoCases_FallsBackToElse()
    {
        var node = new WorkflowNode
        {
            Id = "c",
            Type = "condition",
            Config = JsonDocument.Parse("""{ "cases": [] }""").RootElement.Clone(),
        };
        var context = new NodeRunContext
        {
            Node = node,
            RunInputs = NoInputs,
            Outputs = new Dictionary<string, IReadOnlyDictionary<string, object?>>(),
        };

        var result = await Handler.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("else", result.Data!.NextHandle);
    }

    [Fact]
    public async Task Execute_FirstMatchingCaseWins()
    {
        var config = """
            { "cases": [
                { "handle": "a", "left": "x", "op": "eq", "right": "no" },
                { "handle": "b", "left": "x", "op": "eq", "right": "x" },
                { "handle": "c", "left": "x", "op": "eq", "right": "x" }
            ] }
            """;
        var node = new WorkflowNode
        {
            Id = "c",
            Type = "condition",
            Config = JsonDocument.Parse(config).RootElement.Clone(),
        };
        var context = new NodeRunContext
        {
            Node = node,
            RunInputs = NoInputs,
            Outputs = new Dictionary<string, IReadOnlyDictionary<string, object?>>(),
        };

        var result = await Handler.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal("b", result.Data!.NextHandle);
    }
}
