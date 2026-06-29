using Hify.Contracts.Mcp;
using Hify.Shared.Results;

namespace Hify.Modules.Mcp.Protocol;

/// <summary>
/// MCP 协议客户端抽象（一期为 Streamable HTTP）。把 SDK 细节封在实现内，对上只暴露
/// 握手 / 发现 / 调用三件事。可预期失败返回 <see cref="Result{T}"/>（5xxx 码），调用方取消则 <see cref="OperationCanceledException"/> 冒泡。
/// </summary>
internal interface IMcpProtocolClient
{
    /// <summary>建立连接并完成 initialize 握手，返回服务端自述信息。供连通性测试用。</summary>
    Task<Result<McpServerDescriptor>> InitializeAsync(McpServerConnection connection, CancellationToken cancellationToken);

    /// <summary>发现工具（<c>tools/list</c>，自动翻页）。</summary>
    Task<Result<IReadOnlyList<McpDiscoveredTool>>> ListToolsAsync(McpServerConnection connection, CancellationToken cancellationToken);

    /// <summary>
    /// 调用工具（<c>tools/call</c>）。服务端工具级错误以成功 Result 携带 <see cref="McpToolResult.IsError"/>=true 返回；
    /// 不可达 / 协议错 / 超时等以失败 Result 返回。
    /// </summary>
    Task<Result<McpToolResult>> CallToolAsync(McpServerConnection connection, string toolName, string argumentsJson, CancellationToken cancellationToken);
}
