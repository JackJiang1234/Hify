namespace Hify.Modules.Mcp.Protocol;

/// <summary>从 MCP Server 的 <c>tools/list</c> 发现的单个工具（协议层中立表示，不外泄 SDK 类型）。</summary>
internal sealed record McpDiscoveredTool
{
    /// <summary>工具名（服务端唯一标识）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>工具描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>工具入参 JSON Schema（原样 JSON 字符串）。</summary>
    public string InputSchemaJson { get; init; } = "{}";
}
