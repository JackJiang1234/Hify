using Hify.Shared.Persistence;

namespace Hify.Modules.Conversation.Domain;

/// <summary>
/// 消息实体（会话中的一条消息）。message 是增长最快的表，读历史按单调递增的 <see cref="EntityBase.Id"/>
/// 排序（created_at 为 epoch ms，同毫秒会撞，不作排序键）。
/// 工具相关字段（<see cref="ToolCalls"/>/<see cref="ToolCallId"/>）一期不做工具调用，留空备用（见设计 A）。
/// </summary>
internal sealed class Message : EntityBase
{
    /// <summary>所属会话 Id（-&gt; conversation.conversation）。</summary>
    public long ConversationId { get; set; }

    /// <summary>角色：<c>system</c> | <c>user</c> | <c>assistant</c> | <c>tool</c>（一期仅 user/assistant 落库）。</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>消息内容。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>assistant 请求的工具调用（jsonb 文本，一期恒为 <c>[]</c>）。</summary>
    public string ToolCalls { get; set; } = "[]";

    /// <summary>tool 结果消息回指的调用 Id（一期为空）。</summary>
    public string ToolCallId { get; set; } = string.Empty;

    /// <summary>结束原因（仅 assistant 有意义）：<c>stop</c> | <c>length</c> | <c>error</c>。</summary>
    public string FinishReason { get; set; } = string.Empty;

    /// <summary>消息状态：<c>streaming</c> | <c>completed</c> | <c>failed</c> | <c>cancelled</c>。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>失败原因（截断、不含凭证/PII）。</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>实际使用的模型 Id（-&gt; model_provider.model；user 消息为 0）。</summary>
    public long ModelId { get; set; }

    /// <summary>输入 token 用量。</summary>
    public long PromptTokens { get; set; }

    /// <summary>输出 token 用量。</summary>
    public long CompletionTokens { get; set; }
}
