namespace Hify.Contracts.ModelProvider;

/// <summary>嵌入请求（供应商无关）。</summary>
public record EmbeddingRequest
{
    /// <summary>待嵌入的文本列表。</summary>
    public IReadOnlyList<string> Inputs { get; init; } = [];
}
