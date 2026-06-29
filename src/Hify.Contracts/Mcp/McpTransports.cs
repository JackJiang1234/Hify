namespace Hify.Contracts.Mcp;

/// <summary>MCP 传输类型常量（存储为 varchar）。与前端、DDL 取值一一对齐。</summary>
public static class McpTransports
{
    /// <summary>Streamable HTTP（一期唯一支持的传输）。</summary>
    public const string StreamableHttp = "streamable_http";
}
