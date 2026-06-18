namespace Hify.Contracts.ModelProvider;

/// <summary>供应商健康状态（脱敏视图）。反映最近一次连通性测试 / 周期探活的结果。</summary>
public record ProviderHealthDto
{
    /// <summary>健康状态，见 <see cref="HealthStatuses"/>。</summary>
    public string Status { get; init; } = HealthStatuses.Unknown;

    /// <summary>最近一次探活延迟（毫秒）。</summary>
    public int LatencyMs { get; init; }

    /// <summary>连续失败次数。</summary>
    public int ConsecutiveFailures { get; init; }

    /// <summary>最近一次错误信息（不含凭证）。</summary>
    public string LastError { get; init; } = string.Empty;

    /// <summary>最近一次探活时刻（epoch ms，0 表示尚未探测）。</summary>
    public long CheckedAt { get; init; }
}
