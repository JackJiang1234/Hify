using Hify.Contracts.Knowledge;
using Hify.Shared.Results;

namespace Hify.Modules.Conversation.Tests.Support;

/// <summary>
/// <see cref="IKnowledgeQuery"/> 的内存替身：返回预置结果并记录最后一次请求，
/// 用于隔离 KnowledgeRetriever 适配器（无需启动 Knowledge 模块 / 向量库）。
/// </summary>
internal sealed class FakeKnowledgeQuery : IKnowledgeQuery
{
    private readonly Result<IReadOnlyList<KnowledgeChunkDto>> _result;

    public FakeKnowledgeQuery(Result<IReadOnlyList<KnowledgeChunkDto>> result) => _result = result;

    public KnowledgeSearchRequest? LastRequest { get; private set; }

    public static FakeKnowledgeQuery Returning(params KnowledgeChunkDto[] chunks) =>
        new(Result<IReadOnlyList<KnowledgeChunkDto>>.Ok(chunks));

    public static FakeKnowledgeQuery Failing(int code) =>
        new(Result<IReadOnlyList<KnowledgeChunkDto>>.Fail(code, "检索失败。"));

    public Task<Result<IReadOnlyList<KnowledgeChunkDto>>> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(_result);
    }
}
