namespace Hify.Contracts.ModelProvider;

/// <summary>对话响应（非流式，供应商无关）。</summary>
public record ChatResponse
{
    /// <summary>助手回复文本。</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>结束原因（如 <c>stop</c> | <c>length</c>）。</summary>
    public string FinishReason { get; init; } = string.Empty;

    /// <summary>输入 token 用量。</summary>
    public long PromptTokens { get; init; }

    /// <summary>输出 token 用量。</summary>
    public long CompletionTokens { get; init; }
}
