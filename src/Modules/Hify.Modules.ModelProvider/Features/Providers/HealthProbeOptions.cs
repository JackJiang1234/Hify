using System.ComponentModel.DataAnnotations;

namespace Hify.Modules.ModelProvider.Features.Providers;

/// <summary>周期健康探活配置。可关、可调间隔。</summary>
internal sealed class HealthProbeOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "ModelProvider:HealthProbe";

    /// <summary>是否启用周期探活。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>探活间隔（秒）。</summary>
    [Range(5, 3600)]
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>启动后首次探活的延迟（秒），避开冷启动高峰。</summary>
    [Range(0, 3600)]
    public int InitialDelaySeconds { get; set; } = 30;
}
