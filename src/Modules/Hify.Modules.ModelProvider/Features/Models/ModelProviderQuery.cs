using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.ModelProvider.Features.Models;

/// <summary>
/// <see cref="IModelProviderQuery"/> 实现：供 Agent/Conversation/Knowledge 解析模型元数据（只读、不含密钥）。
/// </summary>
internal sealed class ModelProviderQuery : IModelProviderQuery
{
    private readonly ModelProviderDbContext _db;

    public ModelProviderQuery(ModelProviderDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Result<ModelDto>> GetModelAsync(long modelId, CancellationToken cancellationToken)
    {
        var model = await _db.Models.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == modelId, cancellationToken);
        return model is null
            ? Result<ModelDto>.Fail((int)ProviderErrorCode.ModelNotFound, "模型不存在。")
            : Result<ModelDto>.Ok(ModelMapping.ToDto(model));
    }

    /// <inheritdoc />
    public async Task<Result<ModelDto>> GetDefaultModelAsync(long providerId, string modelType, CancellationToken cancellationToken)
    {
        var model = await _db.Models.AsNoTracking().FirstOrDefaultAsync(
            entity => entity.ProviderId == providerId
                && entity.ModelType == modelType
                && entity.IsDefault
                && entity.Enabled,
            cancellationToken);

        return model is null
            ? Result<ModelDto>.Fail((int)ProviderErrorCode.ModelNotFound, "该供应商该类型无可用的默认模型。")
            : Result<ModelDto>.Ok(ModelMapping.ToDto(model));
    }
}
