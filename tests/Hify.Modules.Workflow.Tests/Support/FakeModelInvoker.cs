using Hify.Contracts.ModelProvider;
using Hify.Shared.Results;

namespace Hify.Modules.Workflow.Tests.Support;

/// <summary>可脚本化的假 LLM 门面：ChatAsync 按注入委托返回；流式/嵌入不支持（工作流一期不用）。</summary>
internal sealed class FakeModelInvoker : IModelInvoker
{
    private readonly Func<long, ChatRequest, Result<ChatResponse>> _chat;

    public FakeModelInvoker(Func<long, ChatRequest, Result<ChatResponse>> chat) => _chat = chat;

    /// <summary>返回固定文本的便捷构造。</summary>
    public static FakeModelInvoker Returning(string text) =>
        new((_, _) => Result<ChatResponse>.Ok(new ChatResponse { Content = text, FinishReason = "stop" }));

    public Task<Result<ChatResponse>> ChatAsync(long modelId, ChatRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(_chat(modelId, request));

    public Task<Result<IAsyncEnumerable<ChatStreamChunk>>> ChatStreamAsync(
        long modelId, ChatRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Result<EmbeddingResponse>> EmbedAsync(
        long modelId, EmbeddingRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
