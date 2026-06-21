using Hify.Shared.Persistence;

namespace Hify.Modules.Agent.Domain;

/// <summary>
/// Agent 与 MCP 工具的绑定（多对多关联行）。两侧 Id 均为应用层维护的引用，不建库级外键。
/// </summary>
internal sealed class AgentTool : EntityBase
{
    /// <summary>所属 Agent Id（-&gt; agent.agent）。</summary>
    public long AgentId { get; set; }

    /// <summary>绑定的 MCP 工具 Id（-&gt; mcp.mcp_tool）。</summary>
    public long ToolId { get; set; }
}
