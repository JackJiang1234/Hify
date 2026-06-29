namespace Hify.Modules.Mcp.Features.Servers;

/// <summary>MCP Server 连接/发现状态常量（存储为 varchar）。与 DDL 注释取值一一对齐。</summary>
internal static class McpServerStatuses
{
    /// <summary>未探测。</summary>
    public const string Unknown = "unknown";

    /// <summary>握手成功、可连通。</summary>
    public const string Connected = "connected";

    /// <summary>连接 / 发现失败。</summary>
    public const string Error = "error";
}
