namespace Hify.Modules.Mcp.Features.Servers;

/// <summary>
/// MCP Server 管理视图（脱敏）。凭证只出 <see cref="ApiKeyHint"/>。仅管理 API 使用，故不入 Contracts。
/// </summary>
internal sealed record McpServerDto
{
    /// <summary>主键。</summary>
    public long Id { get; init; }

    /// <summary>名称（唯一）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>传输类型（一期固定 streamable_http）。</summary>
    public string Transport { get; init; } = string.Empty;

    /// <summary>Streamable HTTP 端点 URL。</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>鉴权方式。</summary>
    public string AuthType { get; init; } = string.Empty;

    /// <summary>header 鉴权下的头名。</summary>
    public string AuthHeaderName { get; init; } = string.Empty;

    /// <summary>凭证末位明文（仅展示）。</summary>
    public string ApiKeyHint { get; init; } = string.Empty;

    /// <summary>调用超时（毫秒），0=用全局默认。</summary>
    public int TimeoutMs { get; init; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; }

    /// <summary>连接/发现状态：unknown | connected | error。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>最近一次错误（截断、不含凭证）。</summary>
    public string LastError { get; init; } = string.Empty;

    /// <summary>最近一次 tools/list 成功时刻（epoch ms）。</summary>
    public long LastSyncedAt { get; init; }

    /// <summary>已发现工具数。</summary>
    public int ToolCount { get; init; }

    /// <summary>创建时刻（epoch ms）。</summary>
    public long CreatedAt { get; init; }

    /// <summary>最后更新时刻（epoch ms）。</summary>
    public long UpdatedAt { get; init; }
}
