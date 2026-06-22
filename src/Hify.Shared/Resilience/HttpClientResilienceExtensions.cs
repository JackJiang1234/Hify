using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

using Polly;
using Polly.Timeout;

namespace Hify.Shared.Resilience;

/// <summary>
/// 为 Typed HttpClient 装配 Hify 标准弹性管道：舱壁（并发限制）→ 重试（按异常类型区分）
/// → 熔断 → 单次超时。供各提供商模块在注册 HttpClient 时调用。
/// </summary>
public static class HttpClientResilienceExtensions
{
    /// <summary>挂载弹性管道。</summary>
    /// <param name="builder">HttpClient 构建器。</param>
    /// <param name="options">弹性策略配置。</param>
    public static IHttpClientBuilder AddHifyResilience(this IHttpClientBuilder builder, ResilienceOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        builder.AddResilienceHandler($"hify-{builder.Name}", pipeline =>
        {
            // 舱壁隔离：限制对该提供商的并发在途请求，避免单一提供商拖垮整体。
            pipeline.AddConcurrencyLimiter(permitLimit: options.MaxConcurrency, queueLimit: options.QueueLimit);

            // 重试：网络抖动/超时、5xx、429 退避重试；认证失败（401/403）与 4xx 不重试。
            // RetryCount=0 表示禁用重试（如 SSE 流式）——此时不挂重试策略，
            // 因为 HttpRetryStrategyOptions.MaxRetryAttempts 要求 >=1，置 0 会校验失败。
            if (options.RetryCount > 0)
            {
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = options.RetryCount,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(options.RetryBaseDelayMs),
                    ShouldHandle = args => new ValueTask<bool>(ShouldRetry(args.Outcome)),
                });
            }

            // 熔断：采样窗口内失败率超阈值则开断路，隔离期内快速失败。
            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = options.CircuitFailureRatio,
                MinimumThroughput = options.CircuitMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(options.CircuitSamplingSeconds),
                BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakSeconds),
            });

            // 单次尝试超时（置于最内层，超时异常向上触发重试）。
            pipeline.AddTimeout(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds));
        });

        return builder;
    }

    private static bool ShouldRetry(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is not null)
        {
            // 网络抖动与超时可重试；认证等其它异常不重试。
            return outcome.Exception is HttpRequestException or TimeoutRejectedException;
        }

        var response = outcome.Result;
        if (response is null)
        {
            return false;
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return true;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return false;
        }

        return (int)response.StatusCode >= 500;
    }
}
