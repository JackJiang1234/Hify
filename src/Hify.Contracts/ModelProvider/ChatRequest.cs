namespace Hify.Contracts.ModelProvider;

/// <summary>对话请求（供应商无关）。模型由调用方以 modelId 解析，不在此携带。</summary>
public record ChatRequest
{
    /// <summary>多轮消息（含 system）。</summary>
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];

    /// <summary>单次最大输出 token 数。</summary>
    public int MaxTokens { get; init; }

    /// <summary>采样温度（可选；不支持的供应商/模型将忽略）。</summary>
    public double? Temperature { get; init; }

    /// <summary>核采样 top-p（可选）。</summary>
    public double? TopP { get; init; }
}
