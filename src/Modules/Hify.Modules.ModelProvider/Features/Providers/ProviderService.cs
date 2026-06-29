using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Domain;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Shared.Security;
using Hify.Shared.Pagination;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.ModelProvider.Features.Providers;

/// <summary>供应商 CRUD 应用服务。可预期失败返回 <see cref="Result{T}"/>（2xxx），不抛异常。</summary>
internal sealed class ProviderService
{
    private readonly ModelProviderDbContext _db;
    private readonly ICredentialProtector _protector;

    public ProviderService(ModelProviderDbContext db, ICredentialProtector protector)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(protector);
        _db = db;
        _protector = protector;
    }

    public async Task<Result<ProviderDto>> CreateAsync(CreateProviderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _db.Providers.AnyAsync(provider => provider.Name == request.Name, cancellationToken))
        {
            return Result<ProviderDto>.Fail((int)ProviderErrorCode.ProviderNameConflict, "供应商名称已存在。");
        }

        var provider = new Provider
        {
            Name = request.Name,
            ProviderType = request.ProviderType,
            BaseUrl = request.BaseUrl,
            AuthType = request.AuthType,
            AuthHeaderName = request.AuthHeaderName,
            ApiKeyCipher = _protector.Protect(request.ApiKey),
            ApiKeyHint = ApiKeyHint.Of(request.ApiKey),
            Settings = string.IsNullOrWhiteSpace(request.Settings) ? "{}" : request.Settings,
            Enabled = request.Enabled,
        };

        // 同事务建供应商 + 健康行（健康行需供应商生成的 Id）。
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.Providers.Add(provider);
            await _db.SaveChangesAsync(cancellationToken);

            var health = new ProviderHealth { ProviderId = provider.Id, Status = HealthStatuses.Unknown };
            _db.ProviderHealths.Add(health);
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<ProviderDto>.Ok(ProviderMapping.ToDto(provider, health));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ProviderDto>.Fail((int)ProviderErrorCode.ProviderNameConflict, "供应商名称已存在。");
        }
    }

    public async Task<Result<ProviderDto>> GetAsync(long id, CancellationToken cancellationToken)
    {
        var provider = await _db.Providers.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (provider is null)
        {
            return Result<ProviderDto>.Fail((int)ProviderErrorCode.ProviderNotFound, "供应商不存在。");
        }

        var health = await _db.ProviderHealths.AsNoTracking().FirstOrDefaultAsync(entity => entity.ProviderId == id, cancellationToken);
        return Result<ProviderDto>.Ok(ProviderMapping.ToDto(provider, health));
    }

    public async Task<PageResult<ProviderDto>> ListAsync(int page, int size, CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Of(page, size);
        var query = _db.Providers.AsNoTracking();

        var providers = await query.ApplyPage(pageRequest).ToListAsync(cancellationToken);
        var total = pageRequest.IsFirstPage ? await query.CountAsync(cancellationToken) : 0;

        var ids = providers.Select(provider => provider.Id).ToList();
        var healthByProvider = await _db.ProviderHealths.AsNoTracking()
            .Where(health => ids.Contains(health.ProviderId))
            .ToDictionaryAsync(health => health.ProviderId, cancellationToken);

        var items = providers
            .Select(provider => ProviderMapping.ToDto(provider, healthByProvider.GetValueOrDefault(provider.Id)))
            .ToList();

        return PageResult<ProviderDto>.Ok(items, total, pageRequest.Page, pageRequest.Size);
    }

    public async Task<Result<ProviderDto>> UpdateAsync(long id, UpdateProviderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = await _db.Providers.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (provider is null)
        {
            return Result<ProviderDto>.Fail((int)ProviderErrorCode.ProviderNotFound, "供应商不存在。");
        }

        if (provider.Name != request.Name
            && await _db.Providers.AnyAsync(other => other.Name == request.Name && other.Id != id, cancellationToken))
        {
            return Result<ProviderDto>.Fail((int)ProviderErrorCode.ProviderNameConflict, "供应商名称已存在。");
        }

        provider.Name = request.Name;
        provider.ProviderType = request.ProviderType;
        provider.BaseUrl = request.BaseUrl;
        provider.AuthType = request.AuthType;
        provider.AuthHeaderName = request.AuthHeaderName;
        provider.Settings = string.IsNullOrWhiteSpace(request.Settings) ? "{}" : request.Settings;
        provider.Enabled = request.Enabled;

        // 仅当提供了新密钥才重新加密；留空保留原密钥。
        if (request.ApiKey.Length > 0)
        {
            provider.ApiKeyCipher = _protector.Protect(request.ApiKey);
            provider.ApiKeyHint = ApiKeyHint.Of(request.ApiKey);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var health = await _db.ProviderHealths.AsNoTracking().FirstOrDefaultAsync(entity => entity.ProviderId == id, cancellationToken);
        return Result<ProviderDto>.Ok(ProviderMapping.ToDto(provider, health));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var provider = await _db.Providers.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (provider is null)
        {
            return Result<bool>.Fail((int)ProviderErrorCode.ProviderNotFound, "供应商不存在。");
        }

        // 级联软删：供应商 + 其模型 + 健康行（SaveChanges 由 DbContext 转为软删）。
        var models = await _db.Models.Where(model => model.ProviderId == id).ToListAsync(cancellationToken);
        _db.Models.RemoveRange(models);

        var health = await _db.ProviderHealths.FirstOrDefaultAsync(entity => entity.ProviderId == id, cancellationToken);
        if (health is not null)
        {
            _db.ProviderHealths.Remove(health);
        }

        _db.Providers.Remove(provider);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> SetEnabledAsync(long id, bool enabled, CancellationToken cancellationToken)
    {
        var provider = await _db.Providers.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (provider is null)
        {
            return Result<bool>.Fail((int)ProviderErrorCode.ProviderNotFound, "供应商不存在。");
        }

        provider.Enabled = enabled;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }
}
