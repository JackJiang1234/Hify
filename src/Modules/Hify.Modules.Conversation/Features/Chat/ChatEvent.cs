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

    /// <summary>工具调用开始（工具循环中，供前端展示「正在调用工具 X」）。</summary>
    ToolCall,

    /// <summary>工具调用结果（成功/失败）。</summary>
    ToolResult,
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

    /// <summary>工具调用关联 Id（仅 <see cref="ChatEventType.ToolCall"/> / <see cref="ChatEventType.ToolResult"/>）。</summary>
    public string ToolCallId { get; init; } = string.Empty;

    /// <summary>工具名（仅 <see cref="ChatEventType.ToolCall"/> / <see cref="ChatEventType.ToolResult"/>）。</summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>工具是否报错（仅 <see cref="ChatEventType.ToolResult"/>）。</summary>
    public bool ToolIsError { get; init; }

    /// <summary>工具入参 JSON（仅 <see cref="ChatEventType.ToolCall"/>，供前端展开查看）。</summary>
    public string ToolArguments { get; init; } = string.Empty;

    /// <summary>工具返回内容（仅 <see cref="ChatEventType.ToolResult"/>，已截断，供前端展开查看）。</summary>
    public string ToolResultContent { get; init; } = string.Empty;

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

    public static ChatEvent ToolCallStarted(string callId, string toolName, string arguments) => new()
    {
        Type = ChatEventType.ToolCall,
        ToolCallId = callId,
        ToolName = toolName,
        ToolArguments = arguments,
    };

    public static ChatEvent ToolCallResult(string callId, string toolName, bool isError, string content) => new()
    {
        Type = ChatEventType.ToolResult,
        ToolCallId = callId,
        ToolName = toolName,
        ToolIsError = isError,
        ToolResultContent = Truncate(content, ToolResultMaxLength),
    };

    private const int ToolResultMaxLength = 4000;

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];
}
