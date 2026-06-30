using System.Text.Json;

using Hify.Contracts.Mcp;
using Hify.Contracts.ModelProvider;
using Hify.Modules.Workflow.Features.Definitions;
using Hify.Modules.Workflow.Features.Execution;
using Hify.Modules.Workflow.Features.Execution.Nodes;
using Hify.Modules.Workflow.Tests.Support;
using Hify.Shared.Results;

namespace Hify.Modules.Workflow.Tests.Features.Execution;

/// <summary>
/// 执行引擎端到端测试（不连库；LLM/MCP 用假适配器）。
/// 主拓扑：start → llm → condition →(c1) tool → end_tool ／(else) end_else。
/// </summary>
public sealed class WorkflowEngineTests
{
    private const string PipelineJson = """
        {
          "version": "1",
          "nodes": [
            { "id": "s",   "type": "start",     "config": { "inputs": [ { "name": "q", "required": true } ] } },
            { "id": "llm", "type": "llm",       "config": { "modelId": 1, "prompt": "{{s.q}}" } },
            { "id": "cond","type": "condition", "config": { "cases": [ { "handle": "c1", "left": "{{llm.text}}", "op": "contains", "right": "tech" } ] } },
            { "id": "tool","type": "tool",      "config": { "mcpToolId": 1, "args": { "query": "{{s.q}}" } } },
            { "id": "end_tool", "type": "end",  "config": { "output": "{{tool.result}}" } },
            { "id": "end_else", "type": "end",  "config": { "output": "{{llm.text}}" } }
          ],
          "edges": [
            { "id": "e1", "source": "s",    "target": "llm" },
            { "id": "e2", "source": "llm",  "target": "cond" },
            { "id": "e3", "source": "cond", "target": "tool",     "sourceHandle": "c1" },
            { "id": "e4", "source": "cond", "target": "end_else", "sourceHandle": "else" },
            { "id": "e5", "source": "tool", "target": "end_tool" }
          ]
        }
        """;

    private static WorkflowDefinition Parse(string json)
    {
        var result = new DefinitionValidator().Validate(json);
        Assert.Equal(200, result.Code);
        return result.Data!;
    }

    private static WorkflowEngine Engine(IModelInvoker model, IMcpToolInvoker tool)
    {
        var resolver = new VariableResolver();
        INodeHandler[] handlers =
        [
            new StartNodeHandler(),
            new LlmNodeHandler(model, resolver),
            new ToolNodeHandler(tool, resolver),
            new ConditionNodeHandler(resolver),
            new EndNodeHandler(resolver),
        ];
        return new WorkflowEngine(handlers);
    }

    private static IReadOnlyDictionary<string, object?> Inputs(string q) =>
        new Dictionary<string, object?> { ["q"] = q };

    [Fact]
    public async Task Execute_TechBranch_RunsToolAndReturnsItsResult()
    {
        var engine = Engine(FakeModelInvoker.Returning("this is tech"), FakeMcpToolInvoker.Returning("TOOL_OK"));

        var execution = await engine.ExecuteAsync(Parse(PipelineJson), Inputs("hello"), CancellationToken.None);

        Assert.Equal("succeeded", execution.Status);
        Assert.Equal("TOOL_OK", execution.Output);
        Assert.Collection(
            execution.Trace,
            t => Assert.Equal("s", t.NodeId),
            t => Assert.Equal("llm", t.NodeId),
            t => Assert.Equal("cond", t.NodeId),
            t => Assert.Equal("tool", t.NodeId),
            t => Assert.Equal("end_tool", t.NodeId));
    }

    [Fact]
    public async Task Execute_ElseBranch_SkipsToolReturnsLlmText()
    {
        var engine = Engine(FakeModelInvoker.Returning("sales question"), FakeMcpToolInvoker.Returning("UNUSED"));

        var execution = await engine.ExecuteAsync(Parse(PipelineJson), Inputs("hello"), CancellationToken.None);

        Assert.Equal("succeeded", execution.Status);
        Assert.Equal("sales question", execution.Output);
        Assert.Equal(4, execution.Trace.Count);
        Assert.Equal("end_else", execution.Trace[^1].NodeId);
    }

    [Fact]
    public async Task Execute_LlmFailure_ReturnsFailedWith6004()
    {
        var failingModel = new FakeModelInvoker((_, _) => Result<ChatResponse>.Fail(2001, "上游 LLM 拒绝"));
        var engine = Engine(failingModel, FakeMcpToolInvoker.Returning("X"));

        var execution = await engine.ExecuteAsync(Parse(PipelineJson), Inputs("hi"), CancellationToken.None);

        Assert.Equal("failed", execution.Status);
        Assert.Equal(6004, execution.ErrorCode);
        Assert.Equal("failed", execution.Trace[^1].Status);
        Assert.Equal("llm", execution.Trace[^1].NodeId);
    }

    [Fact]
    public async Task Execute_ToolError_ReturnsFailedWith6004()
    {
        var erroringTool = new FakeMcpToolInvoker(_ =>
            Result<Hify.Contracts.Mcp.McpToolResult>.Ok(new Hify.Contracts.Mcp.McpToolResult { Content = "boom", IsError = true }));
        var engine = Engine(FakeModelInvoker.Returning("tech"), erroringTool);

        var execution = await engine.ExecuteAsync(Parse(PipelineJson), Inputs("hi"), CancellationToken.None);

        Assert.Equal("failed", execution.Status);
        Assert.Equal(6004, execution.ErrorCode);
        Assert.Equal("tool", execution.Trace[^1].NodeId);
    }

    [Fact]
    public async Task Execute_RequiredInputMissing_FailsAtStartWith6003()
    {
        var engine = Engine(FakeModelInvoker.Returning("tech"), FakeMcpToolInvoker.Returning("X"));

        var execution = await engine.ExecuteAsync(
            Parse(PipelineJson),
            new Dictionary<string, object?>(),
            CancellationToken.None);

        Assert.Equal("failed", execution.Status);
        Assert.Equal(6003, execution.ErrorCode);
        Assert.Equal("s", execution.Trace[^1].NodeId);
    }

    [Fact]
    public async Task Execute_CyclicGraph_FailsWithMaxSteps6005()
    {
        // 直接构造带环定义（绕过校验器）以验证引擎防失控限步。
        WorkflowNode Node(string id, string type, string config) =>
            new() { Id = id, Type = type, Config = JsonDocument.Parse(config).RootElement.Clone() };

        var definition = new WorkflowDefinition
        {
            Nodes =
            [
                Node("s", "start", """{ "inputs": [] }"""),
                Node("a", "llm", """{ "modelId": 1, "prompt": "x" }"""),
                Node("b", "llm", """{ "modelId": 1, "prompt": "y" }"""),
            ],
            Edges =
            [
                new WorkflowEdge { Id = "e1", Source = "s", Target = "a" },
                new WorkflowEdge { Id = "e2", Source = "a", Target = "b" },
                new WorkflowEdge { Id = "e3", Source = "b", Target = "a" },
            ],
        };

        var engine = Engine(FakeModelInvoker.Returning("ok"), FakeMcpToolInvoker.Returning("ok"));

        var execution = await engine.ExecuteAsync(
            definition,
            new Dictionary<string, object?>(),
            CancellationToken.None);

        Assert.Equal("failed", execution.Status);
        Assert.Equal(6005, execution.ErrorCode);
    }
}
