namespace Hify.Contracts.ModelProvider;

/// <summary>嵌入响应（供应商无关）。<see cref="Vectors"/> 顺序与请求 Inputs 一一对应。</summary>
public record EmbeddingResponse
{
    /// <summary>每条输入对应的向量。</summary>
    public IReadOnlyList<IReadOnlyList<float>> Vectors { get; init; } = [];

    /// <summary>输入 token 用量。</summary>
    public long PromptTokens { get; init; }
}
