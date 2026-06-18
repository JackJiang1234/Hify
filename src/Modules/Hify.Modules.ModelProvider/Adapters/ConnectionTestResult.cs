namespace Hify.Modules.ModelProvider.Adapters;

/// <summary>连通性测试结果。</summary>
internal sealed record ConnectionTestResult
{
    /// <summary>探测延迟（毫秒）。</summary>
    public int LatencyMs { get; init; }
}
