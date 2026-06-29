using Hify.Shared.Persistence;

namespace Hify.Modules.Mcp.Domain;

/// <summary>
/// 从 <see cref="McpServer"/> 发现并缓存的一个工具。通过 <see cref="ServerId"/> 关联（应用层维护，不建库级外键）。
/// 重新发现按 (server_id, name) 原地 upsert：<see cref="EntityBase.Id"/> 永不变，以保护
/// agent.agent_tool.tool_id 对本表的引用稳定。服务端移除某工具时仅置 <see cref="Available"/>=false，
/// 不软删、不换 id；重现则置回 true。
/// </summary>
internal sealed class McpTool : EntityBase
{
    /// <summary>所属 MCP Server Id。</summary>
    public long ServerId { get; set; }

    /// <summary>工具名（同一 server 未删集合内唯一），即 MCP <c>tools/list</c> 返回的工具标识。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>工具描述，喂给模型用于决定何时调用，可能较长。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>工具入参 JSON Schema（jsonb），来自服务端发现结果。</summary>
    public string InputSchema { get; set; } = "{}";

    /// <summary>最近一次发现中服务端是否仍提供该工具（≠ <see cref="Enabled"/>）。</summary>
    public bool Available { get; set; } = true;

    /// <summary>管理员是否启用该工具。</summary>
    public bool Enabled { get; set; } = true;
}
