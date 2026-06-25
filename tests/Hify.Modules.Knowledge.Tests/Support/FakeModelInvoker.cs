using Hify.Contracts.ModelProvider;
using Hify.Shared.Results;

namespace Hify.Modules.Knowledge.Tests.Support;

/// <summary>
/// <see cref="IModelInvoker"/> 的内存替身：仅实现 <see cref="EmbedAsync"/>，按输入条数返回
/// 指定维度的确定性向量；可配置为失败。对话方法不被知识库流程使用，调用即抛。
/// </summary>
internal sealed class FakeModelInvoker : IModelInvoker
{
    private readonly int _dimensions;
    private readonly bool _fail;

    public FakeModelInvoker(int dimensions = 1536, bool fail = false)
    {
        _dimensions = dimensions;
        _fail = fail;
    }

    public Task<Result<EmbeddingResponse>> EmbedAsync(long modelId, EmbeddingRequest request, CancellationToken cancellationToken)
    {
        if (_fail)
        {
            return Task.FromResult(Result<EmbeddingResponse>.Fail(2010, "嵌入失败。"));
        }

        var vectors = request.Inputs
            .Select(BuildVector)
            .ToList();
        return Task.FromResult(Result<EmbeddingResponse>.Ok(new EmbeddingResponse { Vectors = vectors, PromptTokens = 0 }));
    }

    // 仅由文本内容确定性生成向量：相同文本 => 相同向量（余弦距离 0），不同文本 => 不同向量。
    // 这样"查询命中与之文本相同的分块"可断言其排在最前、相似度最高。
    private IReadOnlyList<float> BuildVector(string input)
    {
        var seed = 0;
        foreach (var ch in input)
        {
            seed = unchecked((seed * 31) + ch);
        }

        var random = new Random(seed);
        var values = new float[_dimensions];
        for (var k = 0; k < _dimensions; k++)
        {
            values[k] = (float)random.NextDouble();
        }

        return values;
    }

    public Task<Result<ChatResponse>> ChatAsync(long modelId, ChatRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("知识库流程不使用对话调用。");

    public Task<Result<IAsyncEnumerable<ChatStreamChunk>>> ChatStreamAsync(long modelId, ChatRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("知识库流程不使用对话调用。");
}
