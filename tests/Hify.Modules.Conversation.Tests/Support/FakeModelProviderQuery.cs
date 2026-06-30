using Hify.Contracts.ModelProvider;
using Hify.Shared.Results;

namespace Hify.Modules.Conversation.Tests.Support;

/// <summary>
/// <see cref="IModelProviderQuery"/> 的内存替身：按 Id 返回预置模型元数据，未预置返回 NotFound。
/// </summary>
internal sealed class FakeModelProviderQuery : IModelProviderQuery
{
    private readonly Dictionary<long, ModelDto> _models = [];

    public FakeModelProviderQuery Add(ModelDto model)
    {
        _models[model.Id] = model;
        return this;
    }

    public static ModelDto ChatModel(
        long id,
        bool enabled = true,
        long contextWindow = 8192,
        long maxOutputTokens = 1024,
        bool supportsTools = false) => new()
    {
        Id = id,
        ModelType = ModelTypes.Chat,
        Enabled = enabled,
        SupportsStreaming = true,
        SupportsTools = supportsTools,
        ContextWindow = contextWindow,
        MaxOutputTokens = maxOutputTokens,
    };

    public Task<Result<ModelDto>> GetModelAsync(long modelId, CancellationToken cancellationToken) =>
        Task.FromResult(_models.TryGetValue(modelId, out var model)
            ? Result<ModelDto>.Ok(model)
            : Result<ModelDto>.Fail(2009, "模型不存在。"));

    public Task<Result<ModelDto>> GetDefaultModelAsync(long providerId, string modelType, CancellationToken cancellationToken) =>
        throw new NotSupportedException("对话引擎不使用默认模型解析。");
}
