namespace Hify.Contracts.Mcp;

/// <summary>
/// MCP 工具元数据。供 Conversation/Workflow 据此构造发给 LLM 的工具定义，
/// 与管理 API 返回共用。<see cref="Id"/> 为稳定引用（Agent 绑定即引用此 Id）。
/// </summary>
public record McpToolDto
{
    /// <summary>主键（稳定引用，工具重新发现也不变）。</summary>
    public long Id { get; init; }

    /// <summary>所属 MCP Server Id。</summary>
    public long ServerId { get; init; }

    /// <summary>工具名（同一 Server 内唯一），即 MCP <c>tools/list</c> 的工具标识。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>工具描述（喂给模型用于决定何时调用）。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>工具入参 JSON Schema（原样 JSON 字符串）。</summary>
    public string InputSchema { get; init; } = "{}";

    /// <summary>最近一次发现中服务端是否仍提供该工具。</summary>
    public bool Available { get; init; }

    /// <summary>管理员是否启用该工具。</summary>
    public bool Enabled { get; init; }
}
