using Microsoft.Extensions.Options;

using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;

namespace Hify.Modules.Mcp.Protocol;

/// <summary>
/// 按 serverId 提供独立的弹性管道（舱壁 + 熔断），实现「每个 Server 熔断器 + 舱壁隔离」。
/// 管道按 serverId 缓存复用——同一 Server 的并发计数与熔断状态须跨调用累积，故不可每次新建。
/// 超时不在此（每调用经 CancellationToken 应用，以支持 Server 行级 timeout_ms 覆盖）。
/// </summary>
internal sealed class McpResiliencePipelineProvider : IDisposable
{
    private readonly ResiliencePipelineRegistry<long> _registry = new();
    private readonly McpPerServerResilienceOptions _options;

    public McpResiliencePipelineProvider(IOptions<McpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value.PerServer;
    }

    /// <summary>取得该 Server 的弹性管道；首次访问按配置构建并缓存，后续返回同一实例。</summary>
    /// <param name="serverId">MCP Server Id。</param>
    public ResiliencePipeline GetPipeline(long serverId) =>
        _registry.GetOrAddPipeline(serverId, Configure);

    private void Configure(ResiliencePipelineBuilder builder)
    {
        // 舱壁：限制对该 Server 的并发在途调用，避免单一 Server 拖垮整体。
        builder.AddConcurrencyLimiter(permitLimit: _options.MaxConcurrency, queueLimit: _options.QueueLimit);

        // 熔断：采样窗口内失败率超阈值则开断路，隔离期内快速失败、到期半开试探。
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = _options.CircuitFailureRatio,
            MinimumThroughput = _options.CircuitMinimumThroughput,
            SamplingDuration = TimeSpan.FromSeconds(_options.CircuitSamplingSeconds),
            BreakDuration = TimeSpan.FromSeconds(_options.CircuitBreakSeconds),
        });
    }

    public void Dispose() => _registry.Dispose();
}
