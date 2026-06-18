using Hify.Shared.Persistence;

namespace Hify.Modules.ModelProvider.Domain;

/// <summary>
/// 供应商健康状态，与 <see cref="Provider"/> 1:1。独立成表以隔离高频探活写与可缓存的配置行。
/// 仅记录连通性测试 / 周期探活的结果；运行时熔断状态在内存，不落本表。
/// </summary>
internal sealed class ProviderHealth : EntityBase
{
    /// <summary>所属供应商 Id（1:1）。</summary>
    public long ProviderId { get; set; }

    /// <summary>健康状态：<c>unknown</c> | <c>healthy</c> | <c>unhealthy</c>。</summary>
    public string Status { get; set; } = "unknown";

    /// <summary>最近一次探活延迟（毫秒）。</summary>
    public int LatencyMs { get; set; }

    /// <summary>连续失败计数，辅助判定 <c>unhealthy</c>。</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>最近一次错误信息（截断、不含凭证）。</summary>
    public string LastError { get; set; } = string.Empty;

    /// <summary>最近一次探活时刻（epoch ms）。</summary>
    public long CheckedAt { get; set; }
}
