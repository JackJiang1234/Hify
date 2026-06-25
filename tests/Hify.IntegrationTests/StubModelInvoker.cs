using Hify.Contracts.ModelProvider;
using Hify.Shared.Results;

namespace Hify.IntegrationTests;

/// <summary>
/// 集成测试用的 <see cref="IModelInvoker"/> 桩：替换真实 LLM 调用边界，使知识库上传/检索可端到端跑通而不触网。
/// 嵌入按输入内容确定性生成 1536 维向量（相同文本得相同向量，便于断言"查询命中相同文本的分块"）。
/// 对话方法不被知识库流程使用，调用即抛。
/// </summary>
internal sealed class StubModelInvoker : IModelInvoker
{
    private const int Dimensions = 1536;

    public Task<Result<EmbeddingResponse>> EmbedAsync(long modelId, EmbeddingRequest request, CancellationToken cancellationToken)
    {
        var vectors = request.Inputs.Select(BuildVector).ToList();
        return Task.FromResult(Result<EmbeddingResponse>.Ok(new EmbeddingResponse { Vectors = vectors, PromptTokens = 0 }));
    }

    private static IReadOnlyList<float> BuildVector(string input)
    {
        var seed = 0;
        foreach (var ch in input)
        {
            seed = unchecked((seed * 31) + ch);
        }

        var random = new Random(seed);
        var values = new float[Dimensions];
        for (var k = 0; k < Dimensions; k++)
        {
            values[k] = (float)random.NextDouble();
        }

        return values;
    }

    public Task<Result<ChatResponse>> ChatAsync(long modelId, ChatRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("集成测试桩不支持对话调用。");

    public Task<Result<IAsyncEnumerable<ChatStreamChunk>>> ChatStreamAsync(long modelId, ChatRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("集成测试桩不支持对话调用。");
}
