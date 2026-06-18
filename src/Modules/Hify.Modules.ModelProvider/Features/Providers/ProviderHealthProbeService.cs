using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hify.Modules.ModelProvider.Features.Providers;

/// <summary>
/// 周期健康探活后台服务：按配置间隔触发，每轮新建 DI scope 解析 <see cref="ProviderHealthProbe"/> 执行一轮。
/// 可经配置禁用；单轮失败不退出循环；停机时随取消令牌优雅退出。
/// </summary>
internal sealed class ProviderHealthProbeService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HealthProbeOptions _options;
    private readonly ILogger<ProviderHealthProbeService> _logger;

    public ProviderHealthProbeService(
        IServiceScopeFactory scopeFactory,
        IOptions<HealthProbeOptions> options,
        ILogger<ProviderHealthProbeService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("供应商周期探活已禁用（{Section}:Enabled=false）。", HealthProbeOptions.SectionName);
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds));
        try
        {
            if (_options.InitialDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.InitialDelaySeconds), stoppingToken);
            }

            do
            {
                await ProbeOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // 停机，正常退出。
        }
    }

    private async Task ProbeOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var probe = scope.ServiceProvider.GetRequiredService<ProviderHealthProbe>();
            var count = await probe.ProbeAllAsync(cancellationToken);
            _logger.LogDebug("周期探活完成：{Count} 个供应商。", count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "周期探活迭代失败。");
        }
    }
}
