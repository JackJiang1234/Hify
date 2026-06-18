using Hify.Shared.Results;

namespace Hify.Contracts.ModelProvider;

/// <summary>
/// ModelProvider 模块对外公开的 LLM 调用门面：以 modelId 调用对话/嵌入，
/// 内部解析供应商、解密密钥、选择适配器。凭证不出模块。供 Conversation/Knowledge 等模块使用。
/// </summary>
public interface IModelInvoker
{
    /// <summary>非流式对话。模型/供应商不存在或停用、密钥异常等以失败 <see cref="Result{T}"/>（2xxx）返回。</summary>
    /// <param name="modelId">模型 Id。</param>
    /// <param name="request">对话请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<ChatResponse>> ChatAsync(long modelId, ChatRequest request, CancellationToken cancellationToken);

    /// <summary>流式对话。初始解析/请求失败以 <see cref="Result{T}"/> 返回；流中途异常则抛出。</summary>
    /// <param name="modelId">模型 Id。</param>
    /// <param name="request">对话请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<IAsyncEnumerable<ChatStreamChunk>>> ChatStreamAsync(long modelId, ChatRequest request, CancellationToken cancellationToken);

    /// <summary>文本嵌入。</summary>
    /// <param name="modelId">嵌入模型 Id。</param>
    /// <param name="request">嵌入请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<EmbeddingResponse>> EmbedAsync(long modelId, EmbeddingRequest request, CancellationToken cancellationToken);
}
