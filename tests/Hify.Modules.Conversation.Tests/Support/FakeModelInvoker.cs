using Hify.Contracts.ModelProvider;
using Hify.Shared.Results;

namespace Hify.Modules.Conversation.Tests.Support;

/// <summary>
/// <see cref="IModelInvoker"/> 的脚本化替身：按预置脚本逐片吐 <see cref="ChatStreamChunk"/>，
/// 可模拟正常结束、初始失败、流中途抛异常、以及响应取消。无需真实 LLM。
/// </summary>
internal sealed class FakeModelInvoker : IModelInvoker
{
    private readonly IReadOnlyList<string> _deltas;
    private readonly bool _initialFailure;
    private readonly bool _throwMidStream;
    private readonly string _finishReason;
    private readonly long _promptTokens;
    private readonly long _completionTokens;

    private FakeModelInvoker(
        IReadOnlyList<string> deltas,
        bool initialFailure,
        bool throwMidStream,
        string finishReason,
        long promptTokens,
        long completionTokens)
    {
        _deltas = deltas;
        _initialFailure = initialFailure;
        _throwMidStream = throwMidStream;
        _finishReason = finishReason;
        _promptTokens = promptTokens;
        _completionTokens = completionTokens;
    }

    public static FakeModelInvoker Streaming(params string[] deltas) =>
        new(deltas, initialFailure: false, throwMidStream: false, "stop", promptTokens: 11, completionTokens: 7);

    public static FakeModelInvoker InitialFailure() =>
        new([], initialFailure: true, throwMidStream: false, string.Empty, 0, 0);

    public static FakeModelInvoker ThrowsMidStream(params string[] deltas) =>
        new(deltas, initialFailure: false, throwMidStream: true, string.Empty, 0, 0);

    public Task<Result<ChatResponse>> ChatAsync(long modelId, ChatRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("对话引擎一期仅用流式。");

    public Task<Result<IAsyncEnumerable<ChatStreamChunk>>> ChatStreamAsync(long modelId, ChatRequest request, CancellationToken cancellationToken)
    {
        if (_initialFailure)
        {
            return Task.FromResult(Result<IAsyncEnumerable<ChatStreamChunk>>.Fail(2010, "上游不可用。"));
        }

        return Task.FromResult(Result<IAsyncEnumerable<ChatStreamChunk>>.Ok(Generate(cancellationToken)));
    }

    public Task<Result<EmbeddingResponse>> EmbedAsync(long modelId, EmbeddingRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("对话引擎不调用嵌入。");

    private async IAsyncEnumerable<ChatStreamChunk> Generate(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var delta in _deltas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatStreamChunk { Delta = delta };
            await Task.Yield();
        }

        if (_throwMidStream)
        {
            throw new InvalidOperationException("模拟上游流中途错误。");
        }

        yield return new ChatStreamChunk
        {
            IsFinal = true,
            FinishReason = _finishReason,
            PromptTokens = _promptTokens,
            CompletionTokens = _completionTokens,
        };
    }
}
