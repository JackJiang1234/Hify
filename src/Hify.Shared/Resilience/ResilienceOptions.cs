using System.ComponentModel.DataAnnotations;

namespace Hify.Shared.Resilience;

/// <summary>
/// 外部 HTTP 调用的弹性策略配置。每个提供商可绑定各自配置节，实现独立的熔断与舱壁隔离。
/// 默认值对齐规范：同步调用超时 60s、网络抖动/限流退避重试、认证失败不重试。
/// </summary>
public sealed class ResilienceOptions
{
    /// <summary>单次尝试超时（秒）。同步调用默认 60s；SSE 流式应另设更大值（如 120s）。</summary>
    [Range(1, 600)]
    public int AttemptTimeoutSeconds { get; set; } = 60;

    /// <summary>最大重试次数（不含首次）。仅对可重试错误生效。</summary>
    [Range(0, 10)]
    public int RetryCount { get; set; } = 2;

    /// <summary>重试基础退避（毫秒），指数退避 + 抖动的基数。</summary>
    [Range(1, 60000)]
    public int RetryBaseDelayMs { get; set; } = 200;

    /// <summary>并发许可上限（舱壁隔离），限制对单一提供商的并发在途请求数。</summary>
    [Range(1, 10000)]
    public int MaxConcurrency { get; set; } = 50;

    /// <summary>并发队列上限。0 表示超出并发即快速失败、不排队。</summary>
    [Range(0, 10000)]
    public int QueueLimit { get; set; }

    /// <summary>熔断失败比例阈值（0..1）。采样窗口内失败率达到即开断路。</summary>
    [Range(0.1, 1.0)]
    public double CircuitFailureRatio { get; set; } = 0.5;

    /// <summary>熔断最小吞吐量。采样窗口内请求数低于此值不触发熔断。</summary>
    [Range(2, 1000)]
    public int CircuitMinimumThroughput { get; set; } = 10;

    /// <summary>熔断采样窗口（秒）。</summary>
    [Range(1, 600)]
    public int CircuitSamplingSeconds { get; set; } = 30;

    /// <summary>熔断打开后的隔离时长（秒），其间快速失败，到期后半开试探。</summary>
    [Range(1, 600)]
    public int CircuitBreakSeconds { get; set; } = 15;
}
