namespace Hify.Contracts.Mcp;

/// <summary>
/// 工具调用结果。MCP 返回的 content[] 已拍平为文本以喂回 LLM；
/// <see cref="IsError"/> 透传服务端的工具级错误标志（区别于调用层失败的 <c>Result.Fail</c>）。
/// </summary>
public record McpToolResult
{
    /// <summary>结果文本（content[] 拍平）。</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>是否为工具级错误（服务端 isError=true）。调用本身成功，但工具执行报错。</summary>
    public bool IsError { get; init; }
}
