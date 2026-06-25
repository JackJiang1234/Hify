using Hify.Contracts.ModelProvider;
using Hify.Shared.Results;

namespace Hify.Modules.Knowledge.Tests.Support;

/// <summary>
/// <see cref="IModelProviderQuery"/> 的内存替身：按 Id 返回预置模型元数据，未预置则返回 NotFound。
/// 用于隔离知识库的嵌入模型引用校验（含 1536 维约束），无需启动 ModelProvider 模块。
/// </summary>
internal sealed class FakeModelProviderQuery : IModelProviderQuery
{
    private readonly Dictionary<long, ModelDto> _models = [];

    public FakeModelProviderQuery Add(ModelDto model)
    {
        _models[model.Id] = model;
        return this;
    }

    public static ModelDto EmbeddingModel(long id, bool enabled = true, int dimensions = 1536) => new()
    {
        Id = id,
        ModelType = ModelTypes.Embedding,
        Enabled = enabled,
        EmbeddingDimensions = dimensions,
    };

    public Task<Result<ModelDto>> GetModelAsync(long modelId, CancellationToken cancellationToken) =>
        Task.FromResult(_models.TryGetValue(modelId, out var model)
            ? Result<ModelDto>.Ok(model)
            : Result<ModelDto>.Fail(2009, "模型不存在。"));

    public Task<Result<ModelDto>> GetDefaultModelAsync(long providerId, string modelType, CancellationToken cancellationToken) =>
        throw new NotSupportedException("知识库服务不使用默认模型解析。");
}
