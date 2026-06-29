using Hify.Shared.Results;

namespace Hify.Contracts.Mcp;

/// <summary>
/// MCP 模块对外公开的只读查询能力，供 Conversation/Workflow 解析 Agent 绑定的工具元数据，
/// 据此构造发给 LLM 的工具定义。凭证不出模块；实际调用走 <see cref="IMcpToolInvoker"/>。
/// </summary>
public interface IMcpToolQuery
{
    /// <summary>
    /// 按工具 Id 批量获取可调用的工具元数据（仅返回 <c>enabled &amp;&amp; available</c> 的工具，
    /// 停用/已被服务端移除的不返回）。结果顺序不保证；不存在的 Id 直接略过。
    /// </summary>
    /// <param name="toolIds">工具 Id 列表（通常为某 Agent 绑定的工具）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<IReadOnlyList<McpToolDto>>> GetInvocableToolsAsync(
        IReadOnlyList<long> toolIds, CancellationToken cancellationToken);
}
