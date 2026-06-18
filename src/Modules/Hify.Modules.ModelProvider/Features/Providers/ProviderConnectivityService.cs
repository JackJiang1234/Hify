using System.Security.Cryptography;

using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Adapters;
using Hify.Modules.ModelProvider.Domain;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Modules.ModelProvider.Security;
using Hify.Shared.Results;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.ModelProvider.Features.Providers;

/// <summary>
/// 供应商连通性测试：解密密钥 → 选适配器 → 探活 → 把结果写入 provider_health。
/// 除「供应商不存在」外一律返回 Ok(健康快照)（探活/解密失败记为 unhealthy），供「测试」按钮始终拿到状态。
/// 周期探活（P5-9）将复用本服务。
/// </summary>
internal sealed class ProviderConnectivityService
{
    private const int LastErrorMaxLength = 512;

    private readonly ModelProviderDbContext _db;
    private readonly ICredentialProtector _protector;
    private readonly IModelProviderAdapterFactory _adapterFactory;
    private readonly IClock _clock;

    public ProviderConnectivityService(
        ModelProviderDbContext db,
        ICredentialProtector protector,
        IModelProviderAdapterFactory adapterFactory,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(adapterFactory);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _protector = protector;
        _adapterFactory = adapterFactory;
        _clock = clock;
    }

    public async Task<Result<ProviderHealthDto>> TestConnectionAsync(long providerId, CancellationToken cancellationToken)
    {
        var provider = await _db.Providers.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == providerId, cancellationToken);
        if (provider is null)
        {
            return Result<ProviderHealthDto>.Fail((int)ProviderErrorCode.ProviderNotFound, "供应商不存在。");
        }

        var health = await _db.ProviderHealths.FirstOrDefaultAsync(entity => entity.ProviderId == providerId, cancellationToken);
        if (health is null)
        {
            health = new ProviderHealth { ProviderId = providerId };
            _db.ProviderHealths.Add(health);
        }

        string apiKey;
        try
        {
            apiKey = _protector.Unprotect(provider.ApiKeyCipher);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return await RecordFailureAsync(health, "密钥解密失败", cancellationToken);
        }

        IModelProviderAdapter adapter;
        try
        {
            adapter = _adapterFactory.Get(provider.ProviderType);
        }
        catch (NotSupportedException ex)
        {
            return await RecordFailureAsync(health, ex.Message, cancellationToken);
        }

        var connection = new ProviderConnection
        {
            ProviderType = provider.ProviderType,
            BaseUrl = provider.BaseUrl,
            AuthType = provider.AuthType,
            AuthHeaderName = provider.AuthHeaderName,
            ApiKey = apiKey,
            Settings = provider.Settings,
        };

        var probe = await adapter.TestConnectionAsync(connection, cancellationToken);
        if (probe.Code != 200)
        {
            return await RecordFailureAsync(health, probe.Message, cancellationToken);
        }

        health.Status = HealthStatuses.Healthy;
        health.LatencyMs = probe.Data!.LatencyMs;
        health.ConsecutiveFailures = 0;
        health.LastError = string.Empty;
        health.CheckedAt = _clock.UtcNowEpochMs;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<ProviderHealthDto>.Ok(ProviderMapping.ToHealthDto(health));
    }

    private async Task<Result<ProviderHealthDto>> RecordFailureAsync(ProviderHealth health, string message, CancellationToken cancellationToken)
    {
        health.Status = HealthStatuses.Unhealthy;
        health.LatencyMs = 0;
        health.ConsecutiveFailures += 1;
        health.LastError = Truncate(message, LastErrorMaxLength);
        health.CheckedAt = _clock.UtcNowEpochMs;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<ProviderHealthDto>.Ok(ProviderMapping.ToHealthDto(health));
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;
}
