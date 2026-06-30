using System.Text.Json;

namespace Hify.Modules.Conversation.Features.Chat;

/// <summary>
/// 把 <see cref="ChatEvent"/> 序列化为 SSE 帧的 data 负载（camelCase JSON，见对话引擎设计 §6）。
/// 自包含用 System.Text.Json，不依赖 Host 的 MVC 序列化管线——SSE 帧体与 Result 信封无关。
/// </summary>
internal static class ChatEventSerializer
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Serialize(ChatEvent ev) => ev.Type switch
    {
        ChatEventType.Delta => JsonSerializer.Serialize(new { type = "delta", text = ev.Text }, Options),
        ChatEventType.Done => JsonSerializer.Serialize(
            new
            {
                type = "done",
                messageId = ev.MessageId,
                finishReason = ev.FinishReason,
                promptTokens = ev.PromptTokens,
                completionTokens = ev.CompletionTokens,
            },
            Options),
        ChatEventType.Error => JsonSerializer.Serialize(new { type = "error", code = ev.ErrorCode, message = ev.ErrorMessage }, Options),
        ChatEventType.ToolCall => JsonSerializer.Serialize(new { type = "tool_call", callId = ev.ToolCallId, tool = ev.ToolName, arguments = ev.ToolArguments }, Options),
        ChatEventType.ToolResult => JsonSerializer.Serialize(new { type = "tool_result", callId = ev.ToolCallId, tool = ev.ToolName, isError = ev.ToolIsError, result = ev.ToolResultContent }, Options),
        _ => "{}",
    };
}
