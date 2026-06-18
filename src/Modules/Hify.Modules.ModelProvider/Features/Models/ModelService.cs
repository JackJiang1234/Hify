using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

using DomainModel = Hify.Modules.ModelProvider.Domain.Model;

namespace Hify.Modules.ModelProvider.Features.Models;

/// <summary>模型管理应用服务（手动录入）。可预期失败返回 <see cref="Result{T}"/>（2xxx）。</summary>
internal sealed class ModelService
{
    private readonly ModelProviderDbContext _db;

    public ModelService(ModelProviderDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<ModelDto>> CreateAsync(long providerId, CreateModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await _db.Providers.AnyAsync(provider => provider.Id == providerId, cancellationToken))
        {
            return Result<ModelDto>.Fail((int)ProviderErrorCode.ProviderNotFound, "供应商不存在。");
        }

        if (await _db.Models.AnyAsync(model => model.ProviderId == providerId && model.Name == request.Name, cancellationToken))
        {
            return Result<ModelDto>.Fail((int)ProviderErrorCode.ModelNameConflict, "该供应商下已存在同名模型。");
        }

        var model = new DomainModel
        {
            ProviderId = providerId,
            Name = request.Name,
            DisplayName = request.DisplayName,
            ModelType = request.ModelType,
            ContextWindow = request.ContextWindow,
            MaxOutputTokens = request.MaxOutputTokens,
            EmbeddingDimensions = request.EmbeddingDimensions,
            SupportsStreaming = request.SupportsStreaming,
            SupportsTools = request.SupportsTools,
            SupportsVision = request.SupportsVision,
            Source = ModelSources.Manual,
            SortOrder = request.SortOrder,
            Enabled = request.Enabled,
        };

        _db.Models.Add(model);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<ModelDto>.Fail((int)ProviderErrorCode.ModelNameConflict, "该供应商下已存在同名模型。");
        }

        return Result<ModelDto>.Ok(ModelMapping.ToDto(model));
    }

    public async Task<Result<ModelDto>> GetAsync(long id, CancellationToken cancellationToken)
    {
        var model = await _db.Models.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        return model is null
            ? Result<ModelDto>.Fail((int)ProviderErrorCode.ModelNotFound, "模型不存在。")
            : Result<ModelDto>.Ok(ModelMapping.ToDto(model));
    }

    public async Task<Result<IReadOnlyList<ModelDto>>> ListByProviderAsync(long providerId, CancellationToken cancellationToken)
    {
        var models = await _db.Models.AsNoTracking()
            .Where(model => model.ProviderId == providerId)
            .OrderBy(model => model.SortOrder)
            .ThenBy(model => model.Id)
            .ToListAsync(cancellationToken);

        IReadOnlyList<ModelDto> items = models.Select(ModelMapping.ToDto).ToList();
        return Result<IReadOnlyList<ModelDto>>.Ok(items);
    }

    public async Task<Result<ModelDto>> UpdateAsync(long id, UpdateModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var model = await _db.Models.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (model is null)
        {
            return Result<ModelDto>.Fail((int)ProviderErrorCode.ModelNotFound, "模型不存在。");
        }

        if (model.Name != request.Name
            && await _db.Models.AnyAsync(other => other.ProviderId == model.ProviderId && other.Name == request.Name && other.Id != id, cancellationToken))
        {
            return Result<ModelDto>.Fail((int)ProviderErrorCode.ModelNameConflict, "该供应商下已存在同名模型。");
        }

        model.Name = request.Name;
        model.DisplayName = request.DisplayName;
        model.ModelType = request.ModelType;
        model.ContextWindow = request.ContextWindow;
        model.MaxOutputTokens = request.MaxOutputTokens;
        model.EmbeddingDimensions = request.EmbeddingDimensions;
        model.SupportsStreaming = request.SupportsStreaming;
        model.SupportsTools = request.SupportsTools;
        model.SupportsVision = request.SupportsVision;
        model.SortOrder = request.SortOrder;
        model.Enabled = request.Enabled;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<ModelDto>.Ok(ModelMapping.ToDto(model));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var model = await _db.Models.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (model is null)
        {
            return Result<bool>.Fail((int)ProviderErrorCode.ModelNotFound, "模型不存在。");
        }

        _db.Models.Remove(model);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> SetDefaultAsync(long id, CancellationToken cancellationToken)
    {
        var model = await _db.Models.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (model is null)
        {
            return Result<bool>.Fail((int)ProviderErrorCode.ModelNotFound, "模型不存在。");
        }

        // 部分唯一索引保证每供应商每类型至多一个默认：先清旧默认（单独 SaveChanges），再设新默认。
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var currentDefaults = await _db.Models
            .Where(other => other.ProviderId == model.ProviderId
                && other.ModelType == model.ModelType
                && other.IsDefault
                && other.Id != id)
            .ToListAsync(cancellationToken);
        foreach (var current in currentDefaults)
        {
            current.IsDefault = false;
        }

        await _db.SaveChangesAsync(cancellationToken);

        model.IsDefault = true;
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> SetEnabledAsync(long id, bool enabled, CancellationToken cancellationToken)
    {
        var model = await _db.Models.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (model is null)
        {
            return Result<bool>.Fail((int)ProviderErrorCode.ModelNotFound, "模型不存在。");
        }

        model.Enabled = enabled;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }
}
