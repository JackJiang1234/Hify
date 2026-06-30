namespace Hify.Contracts.ModelProvider;

/// <summary>对话消息（供应商无关）。</summary>
public record ChatMessage
{
    /// <summary>角色：<c>system</c> | <c>user</c> | <c>assistant</c> | <c>tool</c>。</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>消息内容。assistant 发起工具调用时可为空（内容在 <see cref="ToolCalls"/>）。</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>assistant 本轮发起的工具调用；非工具轮为空列表。</summary>
    public IReadOnlyList<ToolCall> ToolCalls { get; init; } = [];

    /// <summary><c>tool</c> 角色消息回指的调用 Id（关联上游某次 <see cref="ToolCalls"/>）；其余角色为空。</summary>
    public string ToolCallId { get; init; } = string.Empty;
}
