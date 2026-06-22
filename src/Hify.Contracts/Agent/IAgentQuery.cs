using Hify.Shared.Results;

namespace Hify.Contracts.Agent;

/// <summary>
/// Agent 模块对外公开的只读查询能力，供对话引擎（Conversation，L2）运行时按 Id 装配 Agent 配置
/// （系统提示词、模型引用、工具/知识库引用 ID）。仅负责取数，不判断启用状态——
/// 是否拒绝停用的 Agent 由调用方按各自错误码决定（<see cref="AgentDto.Enabled"/> 已随 DTO 返回）。
/// </summary>
public interface IAgentQuery
{
    /// <summary>按 Id 获取 Agent 配置（含工具/知识库引用 ID）。不存在（含已软删）返回 <c>NotFound</c>（3001）。</summary>
    /// <param name="agentId">Agent Id。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<AgentDto>> GetAgentAsync(long agentId, CancellationToken cancellationToken);
}
