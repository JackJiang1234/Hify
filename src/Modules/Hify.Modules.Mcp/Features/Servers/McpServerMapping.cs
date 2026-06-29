using Hify.Modules.Mcp.Domain;

namespace Hify.Modules.Mcp.Features.Servers;

/// <summary>实体 → 脱敏 DTO 映射。凭证只出 <see cref="McpServerDto.ApiKeyHint"/>。</summary>
internal static class McpServerMapping
{
    public static McpServerDto ToDto(McpServer server) => new()
    {
        Id = server.Id,
        Name = server.Name,
        Transport = server.Transport,
        Endpoint = server.Endpoint,
        AuthType = server.AuthType,
        AuthHeaderName = server.AuthHeaderName,
        ApiKeyHint = server.ApiKeyHint,
        TimeoutMs = server.TimeoutMs,
        Enabled = server.Enabled,
        Status = server.Status,
        LastError = server.LastError,
        LastSyncedAt = server.LastSyncedAt,
        ToolCount = server.ToolCount,
        CreatedAt = server.CreatedAt,
        UpdatedAt = server.UpdatedAt,
    };
}
