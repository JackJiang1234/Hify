namespace Hify.Contracts.ModelProvider;

/// <summary>流式对话增量片段（供应商无关）。最后一片 <see cref="IsFinal"/> 为真，携带用量与结束原因。</summary>
public record ChatStreamChunk
{
    /// <summary>本片增量文本（最后一片通常为空）。</summary>
    public string Delta { get; init; } = string.Empty;

    /// <summary>是否为最后一片。</summary>
    public bool IsFinal { get; init; }

    /// <summary>结束原因（仅最后一片有意义）。</summary>
    public string FinishReason { get; init; } = string.Empty;

    /// <summary>输入 token 用量（仅最后一片有意义，供应商不返回则为 0）。</summary>
    public long PromptTokens { get; init; }

    /// <summary>输出 token 用量（仅最后一片有意义）。</summary>
    public long CompletionTokens { get; init; }
}
