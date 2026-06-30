namespace Hify.Contracts.ModelProvider;

/// <summary>对话响应（非流式，供应商无关）。</summary>
public record ChatResponse
{
    /// <summary>助手回复文本。</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>结束原因（如 <c>stop</c> | <c>length</c> | <c>tool_calls</c>）。</summary>
    public string FinishReason { get; init; } = string.Empty;

    /// <summary>模型发起的工具调用；<see cref="FinishReason"/> 为 <c>tool_calls</c> 时非空，否则为空列表。</summary>
    public IReadOnlyList<ToolCall> ToolCalls { get; init; } = [];

    /// <summary>输入 token 用量。</summary>
    public long PromptTokens { get; init; }

    /// <summary>输出 token 用量。</summary>
    public long CompletionTokens { get; init; }
}
