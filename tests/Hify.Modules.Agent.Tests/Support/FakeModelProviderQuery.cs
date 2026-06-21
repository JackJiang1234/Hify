using Hify.Contracts.ModelProvider;
using Hify.Shared.Results;

namespace Hify.Modules.Agent.Tests.Support;

/// <summary>
/// <see cref="IModelProviderQuery"/> 的内存替身：按 Id 返回预置模型元数据，未预置则返回 NotFound。
/// 用于隔离 Agent 服务的引用校验（方案 B），无需启动 ModelProvider 模块。
/// </summary>
internal sealed class FakeModelProviderQuery : IModelProviderQuery
{
    private readonly Dictionary<long, ModelDto> _models = [];

    public FakeModelProviderQuery Add(ModelDto model)
    {
        _models[model.Id] = model;
        return this;
    }

    public static ModelDto ChatModel(long id, bool enabled = true, bool supportsTools = true, long maxOutputTokens = 4096) => new()
    {
        Id = id,
        ModelType = ModelTypes.Chat,
        Enabled = enabled,
        SupportsTools = supportsTools,
        MaxOutputTokens = maxOutputTokens,
    };

    public Task<Result<ModelDto>> GetModelAsync(long modelId, CancellationToken cancellationToken) =>
        Task.FromResult(_models.TryGetValue(modelId, out var model)
            ? Result<ModelDto>.Ok(model)
            : Result<ModelDto>.Fail(2009, "模型不存在。"));

    public Task<Result<ModelDto>> GetDefaultModelAsync(long providerId, string modelType, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Agent 服务不使用默认模型解析。");
}
