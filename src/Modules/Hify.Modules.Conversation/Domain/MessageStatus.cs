namespace Hify.Modules.Conversation.Domain;

/// <summary>消息状态常量（对齐 message.status 取值）。</summary>
internal static class MessageStatus
{
    /// <summary>流式生成中（assistant 占位）。</summary>
    public const string Streaming = "streaming";

    /// <summary>已完成。</summary>
    public const string Completed = "completed";

    /// <summary>失败（上游错误等）。</summary>
    public const string Failed = "failed";

    /// <summary>用户中断取消。</summary>
    public const string Cancelled = "cancelled";
}
