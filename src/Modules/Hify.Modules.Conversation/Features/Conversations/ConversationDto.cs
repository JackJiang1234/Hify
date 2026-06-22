namespace Hify.Modules.Conversation.Features.Conversations;

/// <summary>会话视图（模块内 DTO；无其它模块依赖 Conversation，故不上提 Contracts）。</summary>
internal sealed record ConversationDto
{
    /// <summary>主键。</summary>
    public long Id { get; init; }

    /// <summary>绑定的 Agent Id。</summary>
    public long AgentId { get; init; }

    /// <summary>标题（首条用户消息截断生成）。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>创建时刻（epoch ms）。</summary>
    public long CreatedAt { get; init; }

    /// <summary>最后更新时刻（epoch ms）。</summary>
    public long UpdatedAt { get; init; }
}
