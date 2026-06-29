using Hify.Contracts.Mcp;
using Hify.Modules.Mcp.Protocol;
using Hify.Shared.Results;

namespace Hify.Modules.Mcp.Tests.Support;

/// <summary>
/// 协议客户端测试替身（service 层在此 seam 上隔离真实网络）。三个操作各可注入行为，默认返回成功空值。
/// </summary>
internal sealed class FakeProtocolClient : IMcpProtocolClient
{
    public Func<McpServerConnection, CancellationToken, Task<Result<McpServerDescriptor>>>? InitializeHandler { get; set; }

    public Func<McpServerConnection, CancellationToken, Task<Result<IReadOnlyList<McpDiscoveredTool>>>>? ListToolsHandler { get; set; }

    public Func<McpServerConnection, string, string, CancellationToken, Task<Result<McpToolResult>>>? CallToolHandler { get; set; }

    public Task<Result<McpServerDescriptor>> InitializeAsync(McpServerConnection connection, CancellationToken cancellationToken) =>
        InitializeHandler?.Invoke(connection, cancellationToken)
        ?? Task.FromResult(Result<McpServerDescriptor>.Ok(new McpServerDescriptor()));

    public Task<Result<IReadOnlyList<McpDiscoveredTool>>> ListToolsAsync(McpServerConnection connection, CancellationToken cancellationToken) =>
        ListToolsHandler?.Invoke(connection, cancellationToken)
        ?? Task.FromResult(Result<IReadOnlyList<McpDiscoveredTool>>.Ok([]));

    public Task<Result<McpToolResult>> CallToolAsync(McpServerConnection connection, string toolName, string argumentsJson, CancellationToken cancellationToken) =>
        CallToolHandler?.Invoke(connection, toolName, argumentsJson, cancellationToken)
        ?? Task.FromResult(Result<McpToolResult>.Ok(new McpToolResult()));
}
