using Hify.Shared.Persistence;

namespace Hify.Modules.Mcp.Domain;

/// <summary>
/// 一份外部 MCP Server 的接入配置（Client 侧）。一期仅支持 Streamable HTTP 传输。
/// 鉴权差异统一为「注入方式 <see cref="AuthType"/> + 头名 <see cref="AuthHeaderName"/> + 密文 <see cref="ApiKeyCipher"/>」，
/// 与 model_provider.provider 同一套加解密。连接/发现状态低频更新，内联本表（不拆健康表）。
/// 工具清单另存 mcp_tool（1:N）。
/// </summary>
internal sealed class McpServer : EntityBase
{
    /// <summary>用户可见名称（同一未删集合内唯一）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>传输类型，一期固定 <c>streamable_http</c>。预留列以便将来扩展，不改表结构。</summary>
    public string Transport { get; set; } = "streamable_http";

    /// <summary>Streamable HTTP 端点 URL（如 <c>https://host/mcp</c>）。</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>鉴权注入方式：<c>none</c> | <c>bearer</c> | <c>header</c>。</summary>
    public string AuthType { get; set; } = "none";

    /// <summary><c>header</c> 模式下的请求头名（如 <c>x-api-key</c>）；其余模式为空。</summary>
    public string AuthHeaderName { get; set; } = string.Empty;

    /// <summary>加密后的凭证，绝不存明文、绝不入日志。</summary>
    public string ApiKeyCipher { get; set; } = string.Empty;

    /// <summary>凭证末位明文，仅供 UI 展示。</summary>
    public string ApiKeyHint { get; set; } = string.Empty;

    /// <summary>调用超时（毫秒）。<c>0</c>=用 appsettings 全局默认；<c>&gt;0</c>=覆盖。</summary>
    public int TimeoutMs { get; set; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>连接/发现状态：<c>unknown</c> | <c>connected</c> | <c>error</c>。</summary>
    public string Status { get; set; } = "unknown";

    /// <summary>最近一次错误信息（截断、不含凭证）。</summary>
    public string LastError { get; set; } = string.Empty;

    /// <summary>最近一次 <c>tools/list</c> 成功时刻（epoch ms）。</summary>
    public long LastSyncedAt { get; set; }

    /// <summary>已发现工具数（冗余计数，免 COUNT mcp_tool）。</summary>
    public int ToolCount { get; set; }
}
