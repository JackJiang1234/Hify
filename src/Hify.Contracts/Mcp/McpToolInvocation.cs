using Hify.Shared.Results;

namespace Hify.Contracts.Mcp;

/// <summary>
/// 批量调用中单个工具的执行结果。每项携带自己的 <see cref="Result"/>，实现部分失败隔离：
/// 一个工具失败不影响同批其它工具，调用方可把成功结果与失败原因分别回填给 LLM。
/// </summary>
public record McpToolInvocation
{
    /// <summary>调用关联 Id（对应入参 <see cref="McpToolCall.CallId"/>）。</summary>
    public string CallId { get; init; } = string.Empty;

    /// <summary>目标工具 Id。</summary>
    public long ToolId { get; init; }

    /// <summary>该次调用的结果：成功含 <see cref="McpToolResult"/>，失败为 5xxx 错误码。</summary>
    public required Result<McpToolResult> Result { get; init; }
}
