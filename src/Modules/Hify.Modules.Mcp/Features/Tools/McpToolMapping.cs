using Hify.Contracts.Mcp;
using Hify.Modules.Mcp.Domain;

namespace Hify.Modules.Mcp.Features.Tools;

/// <summary>工具实体 → DTO 映射。</summary>
internal static class McpToolMapping
{
    public static McpToolDto ToDto(McpTool tool) => new()
    {
        Id = tool.Id,
        ServerId = tool.ServerId,
        Name = tool.Name,
        Description = tool.Description,
        InputSchema = tool.InputSchema,
        Available = tool.Available,
        Enabled = tool.Enabled,
    };
}
