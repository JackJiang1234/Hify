using Hify.Shared.Results;

namespace Hify.Contracts.Mcp;

/// <summary>
/// MCP 模块对外公开的工具调用门面。内部解析工具→Server、解密凭证、经该 Server 的
/// 熔断/舱壁管道调用，凭证不出模块。供 Conversation/Workflow 的工具循环使用。
/// </summary>
public interface IMcpToolInvoker
{
    /// <summary>
    /// 调用单个工具。工具/Server 不存在或停用、不可达、超时、协议错等以失败 <see cref="Result{T}"/>（5xxx）返回；
    /// 服务端工具级错误以成功 Result 携带 <see cref="McpToolResult.IsError"/>=true 返回。
    /// </summary>
    /// <param name="call">调用请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<McpToolResult>> InvokeAsync(McpToolCall call, CancellationToken cancellationToken);

    /// <summary>
    /// 并发执行一批工具调用（对应 LLM 一回合返回的多个 tool_calls）。逐项隔离失败——
    /// 单个工具失败不影响其它；结果顺序与入参 <paramref name="calls"/> 一致，按 CallId 可回填。
    /// 整体取消（<paramref name="cancellationToken"/>）会向上冒泡。
    /// </summary>
    /// <param name="calls">调用请求列表。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<McpToolInvocation>> InvokeManyAsync(
        IReadOnlyList<McpToolCall> calls, CancellationToken cancellationToken);
}
