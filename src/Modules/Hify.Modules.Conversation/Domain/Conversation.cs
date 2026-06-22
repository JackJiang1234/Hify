using Hify.Shared.Persistence;

namespace Hify.Modules.Conversation.Domain;

/// <summary>
/// 会话实体（一次多轮对话的容器）。仅持有 Agent 引用 Id，引用完整性由应用层维护，不建库级外键。
/// 标题一期由首条用户消息截断生成（见对话引擎设计 D）；消息存于 <see cref="Message"/>。
/// </summary>
internal sealed class Conversation : EntityBase
{
    /// <summary>所属 Agent Id（-&gt; agent.agent）。</summary>
    public long AgentId { get; set; }

    /// <summary>会话标题（新建时为空，首条用户消息后回填）。</summary>
    public string Title { get; set; } = string.Empty;
}
