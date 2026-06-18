using Hify.Modules.ModelProvider.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hify.Modules.ModelProvider.Features.Providers;

/// <summary>
/// 单轮探活逻辑（与定时循环解耦，便于独立测试）：探活所有启用中的供应商，复用 <see cref="ProviderConnectivityService"/>
/// 写各自健康。单个供应商探测异常不影响其余。
/// </summary>
internal sealed class ProviderHealthProbe
{
    private readonly ModelProviderDbContext _db;
    private readonly ProviderConnectivityService _connectivity;
    private readonly ILogger<ProviderHealthProbe> _logger;

    public ProviderHealthProbe(
        ModelProviderDbContext db,
        ProviderConnectivityService connectivity,
        ILogger<ProviderHealthProbe> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(connectivity);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _connectivity = connectivity;
        _logger = logger;
    }

    /// <summary>探活所有启用中的供应商，返回探活数量。</summary>
    public async Task<int> ProbeAllAsync(CancellationToken cancellationToken)
    {
        var providerIds = await _db.Providers.AsNoTracking()
            .Where(provider => provider.Enabled)
            .Select(provider => provider.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in providerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _connectivity.TestConnectionAsync(id, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "周期探活：供应商 {ProviderId} 探测异常", id);
            }
        }

        return providerIds.Count;
    }
}
