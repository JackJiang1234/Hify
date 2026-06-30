using System.Text;
using System.Text.Json;

using Hify.Contracts.Mcp;
using Hify.Modules.Workflow.Domain;
using Hify.Shared.Results;

using NodeOutputs = System.Collections.Generic.IReadOnlyDictionary<
    string,
    System.Collections.Generic.IReadOnlyDictionary<string, object?>>;

namespace Hify.Modules.Workflow.Features.Execution.Nodes;

/// <summary>
/// tool 节点：解析 args 内的变量引用后经 <see cref="IMcpToolInvoker"/> 调用 MCP 工具，输出字段 <c>result</c>。
/// 调用层失败或工具级错误（<see cref="McpToolResult.IsError"/>）均以 6004 返回。
/// </summary>
internal sealed class ToolNodeHandler : INodeHandler
{
    private readonly IMcpToolInvoker _toolInvoker;
    private readonly VariableResolver _resolver;

    /// <summary>构造。</summary>
    /// <param name="toolInvoker">MCP 工具调用门面（Mcp 模块）。</param>
    /// <param name="resolver">变量解析器。</param>
    public ToolNodeHandler(IMcpToolInvoker toolInvoker, VariableResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(toolInvoker);
        ArgumentNullException.ThrowIfNull(resolver);
        _toolInvoker = toolInvoker;
        _resolver = resolver;
    }

    /// <inheritdoc />
    public string NodeType => WorkflowNodeType.Tool;

    /// <inheritdoc />
    public async Task<Result<NodeResult>> ExecuteAsync(NodeRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var config = NodeConfigJson.Read<ToolConfig>(context.Node.Config);
        if (config.McpToolId <= 0)
        {
            return Result<NodeResult>.Fail(
                (int)WorkflowErrorCode.ReferenceUnavailable,
                $"tool 节点 {context.Node.Id} 未配置有效 mcpToolId。");
        }

        var call = new McpToolCall
        {
            ToolId = config.McpToolId,
            CallId = context.Node.Id,
            ArgumentsJson = ResolveArgsJson(config.Args, context.Outputs),
        };

        var result = await _toolInvoker.InvokeAsync(call, cancellationToken).ConfigureAwait(false);
        if (result.Code != 200 || result.Data is null)
        {
            return Result<NodeResult>.Fail(
                (int)WorkflowErrorCode.NodeExecutionFailed,
                $"tool 节点 {context.Node.Id} 调用失败：{result.Message}");
        }

        if (result.Data.IsError)
        {
            return Result<NodeResult>.Fail(
                (int)WorkflowErrorCode.NodeExecutionFailed,
                $"tool 节点 {context.Node.Id} 工具返回错误。");
        }

        return Result<NodeResult>.Ok(NodeResult.Single(NodeOutputField.Result, result.Data.Content));
    }

    // 把 args 对象里的字符串值做变量解析，重建为 JSON 字符串（非字符串值原样保留）。
    private string ResolveArgsJson(JsonElement args, NodeOutputs outputs)
    {
        if (args.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return "{}";
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteResolved(writer, args, outputs);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private void WriteResolved(Utf8JsonWriter writer, JsonElement element, NodeOutputs outputs)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteResolved(writer, property.Value, outputs);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteResolved(writer, item, outputs);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(_resolver.ResolveString(element.GetString() ?? string.Empty, outputs));
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private sealed record ToolConfig
    {
        public long McpToolId { get; init; }

        public JsonElement Args { get; init; }
    }
}
