namespace Hify.Contracts.Mcp;

/// <summary>一次工具调用请求。由 Conversation 从 LLM 返回的 tool_calls 映射而来。</summary>
public record McpToolCall
{
    /// <summary>调用关联 Id（LLM 返回的 tool_call.id），用于把结果回填到对应调用。</summary>
    public string CallId { get; init; } = string.Empty;

    /// <summary>目标工具 Id（Agent 绑定的稳定引用）。</summary>
    public long ToolId { get; init; }

    /// <summary>调用入参（原样 JSON 字符串，来自 LLM，调用方不可信、透传给 Server 校验）。</summary>
    public string ArgumentsJson { get; init; } = "{}";
}
