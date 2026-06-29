using Hify.Contracts.ModelProvider;

namespace Hify.Modules.Mcp.Protocol;

/// <summary>
/// 已解析的 MCP Server 连接信息，含解密后的明文凭证（仅在内存短暂存在，绝不入日志）。
/// 由功能层从 mcp_server 行解密构建后传入协议客户端。鉴权模型复用 ModelProvider 的注入方式。
/// </summary>
internal sealed record McpServerConnection
{
    /// <summary>Streamable HTTP 端点 URL。</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>鉴权注入方式，见 <see cref="AuthTypes"/>。</summary>
    public string AuthType { get; init; } = AuthTypes.None;

    /// <summary><c>header</c> 鉴权下的头名。</summary>
    public string AuthHeaderName { get; init; } = string.Empty;

    /// <summary>明文凭证。</summary>
    public string ApiKey { get; init; } = string.Empty;
}
