namespace Hify.Modules.Mcp;

/// <summary>
/// MCP 模块错误码（5xxx 段）。枚举值即对外返回的四位业务码。
/// 服务端工具级错误（isError）不在此——以成功 Result 携带 IsError 标志返回。
/// </summary>
internal enum McpErrorCode
{
    /// <summary>MCP Server 不存在。</summary>
    McpServerNotFound = 5001,

    /// <summary>MCP Server 已停用。</summary>
    McpServerDisabled = 5002,

    /// <summary>MCP Server 不可达（网络错误 / 连接失败）。</summary>
    McpServerUnreachable = 5003,

    /// <summary>MCP Server 鉴权失败（401/403）。</summary>
    McpAuthFailed = 5004,

    /// <summary>MCP 协议错误（握手失败 / JSON-RPC 响应非法 / 版本不兼容）。</summary>
    McpProtocolError = 5005,

    /// <summary>工具不存在。</summary>
    McpToolNotFound = 5006,

    /// <summary>工具不可调用（已停用或服务端已移除）。</summary>
    McpToolUnavailable = 5007,

    /// <summary>工具调用失败（其它非成功情况）。</summary>
    McpToolCallFailed = 5008,

    /// <summary>工具调用超时。</summary>
    McpToolCallTimeout = 5009,

    /// <summary>MCP Server 名称冲突。</summary>
    McpServerNameConflict = 5010,

    /// <summary>凭证解密失败。</summary>
    CredentialError = 5011,
}
