using Hify.Contracts.ModelProvider;
using Hify.Shared.Results;

namespace Hify.Modules.ModelProvider.Adapters;

/// <summary>
/// 单一供应商类型的调用适配器（裸 HttpClient，方案 B）。按 <see cref="ProviderType"/> 由工厂选择。
/// 可预期失败返回 <see cref="Result{T}"/>（2xxx 码），不抛异常。
/// </summary>
internal interface IModelProviderAdapter
{
    /// <summary>本适配器处理的供应商类型，见 <see cref="ProviderTypes"/>。</summary>
    string ProviderType { get; }

    /// <summary>连通性测试（短超时）。</summary>
    Task<Result<ConnectionTestResult>> TestConnectionAsync(
        ProviderConnection connection,
        CancellationToken cancellationToken);

    /// <summary>非流式对话。</summary>
    Task<Result<ChatResponse>> ChatAsync(
        ProviderConnection connection,
        string model,
        ChatRequest request,
        CancellationToken cancellationToken);

    /// <summary>流式对话。初始请求失败以 <see cref="Result{T}"/> 返回；流中途异常则抛出。</summary>
    Task<Result<IAsyncEnumerable<ChatStreamChunk>>> ChatStreamAsync(
        ProviderConnection connection,
        string model,
        ChatRequest request,
        CancellationToken cancellationToken);

    /// <summary>文本嵌入。不支持嵌入的供应商返回 <c>EmbeddingNotSupported</c>。</summary>
    Task<Result<EmbeddingResponse>> EmbedAsync(
        ProviderConnection connection,
        string model,
        EmbeddingRequest request,
        CancellationToken cancellationToken);
}
