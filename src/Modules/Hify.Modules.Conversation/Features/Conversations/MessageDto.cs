namespace Hify.Modules.Conversation.Features.Conversations;

/// <summary>消息视图（会话历史返回项）。一期不暴露工具字段（无工具调用）。</summary>
internal sealed record MessageDto
{
    /// <summary>主键。</summary>
    public long Id { get; init; }

    /// <summary>所属会话 Id。</summary>
    public long ConversationId { get; init; }

    /// <summary>角色：<c>user</c> | <c>assistant</c>。</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>消息内容。</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>结束原因（仅 assistant）。</summary>
    public string FinishReason { get; init; } = string.Empty;

    /// <summary>状态：<c>completed</c> | <c>failed</c> | <c>cancelled</c>。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>输入 token 用量。</summary>
    public long PromptTokens { get; init; }

    /// <summary>输出 token 用量。</summary>
    public long CompletionTokens { get; init; }

    /// <summary>创建时刻（epoch ms）。</summary>
    public long CreatedAt { get; init; }
}
