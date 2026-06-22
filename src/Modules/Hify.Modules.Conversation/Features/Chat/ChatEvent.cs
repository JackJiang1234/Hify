namespace Hify.Modules.Conversation.Features.Chat;

/// <summary>对话引擎流式输出的内部事件类型。</summary>
internal enum ChatEventType
{
    /// <summary>增量文本片段。</summary>
    Delta,

    /// <summary>正常结束（携带落库的 assistant 消息 Id、结束原因与用量）。</summary>
    Done,

    /// <summary>流中途失败（头已发出，无法再用 Result）。</summary>
    Error,
}

/// <summary>
/// 对话引擎产出的内部事件，由控制器经 SseEventWriter 写成 SSE 帧（见对话引擎设计 §6）。
/// 用工厂方法构造，保证各类型只携带其相关字段。
/// </summary>
internal sealed record ChatEvent
{
    /// <summary>事件类型。</summary>
    public ChatEventType Type { get; init; }

    /// <summary>增量文本（仅 <see cref="ChatEventType.Delta"/>）。</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>落库的 assistant 消息 Id（仅 <see cref="ChatEventType.Done"/>）。</summary>
    public long MessageId { get; init; }

    /// <summary>结束原因（仅 <see cref="ChatEventType.Done"/>）。</summary>
    public string FinishReason { get; init; } = string.Empty;

    /// <summary>输入 token 用量（仅 <see cref="ChatEventType.Done"/>）。</summary>
    public long PromptTokens { get; init; }

    /// <summary>输出 token 用量（仅 <see cref="ChatEventType.Done"/>）。</summary>
    public long CompletionTokens { get; init; }

    /// <summary>错误码（仅 <see cref="ChatEventType.Error"/>）。</summary>
    public int ErrorCode { get; init; }

    /// <summary>错误信息（仅 <see cref="ChatEventType.Error"/>，不含敏感数据）。</summary>
    public string ErrorMessage { get; init; } = string.Empty;

    public static ChatEvent Delta(string text) => new() { Type = ChatEventType.Delta, Text = text };

    public static ChatEvent Done(long messageId, string finishReason, long promptTokens, long completionTokens) => new()
    {
        Type = ChatEventType.Done,
        MessageId = messageId,
        FinishReason = finishReason,
        PromptTokens = promptTokens,
        CompletionTokens = completionTokens,
    };

    public static ChatEvent Error(int code, string message) => new()
    {
        Type = ChatEventType.Error,
        ErrorCode = code,
        ErrorMessage = message,
    };
}
