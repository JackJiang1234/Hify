namespace Hify.Contracts.ModelProvider;

/// <summary>对话消息（供应商无关）。</summary>
public record ChatMessage
{
    /// <summary>角色：<c>system</c> | <c>user</c> | <c>assistant</c>。</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>消息内容。</summary>
    public string Content { get; init; } = string.Empty;
}
