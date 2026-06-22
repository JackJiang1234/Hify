namespace Hify.Modules.Conversation.Domain;

/// <summary>消息角色常量（对齐 message.role 取值与供应商无关的 ChatMessage.Role）。</summary>
internal static class MessageRoles
{
    /// <summary>系统提示词（不落库，运行时装配）。</summary>
    public const string System = "system";

    /// <summary>用户消息。</summary>
    public const string User = "user";

    /// <summary>助手回复。</summary>
    public const string Assistant = "assistant";

    /// <summary>工具结果（一期不使用）。</summary>
    public const string Tool = "tool";
}
