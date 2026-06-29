using System.ComponentModel.DataAnnotations;

namespace Hify.Modules.Mcp;

/// <summary>MCP 模块运行期配置（绑定 appsettings <c>Mcp</c> 节）。外部调用相关参数全在此，禁硬编码。</summary>
internal sealed class McpOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "Mcp";

    /// <summary>每个 Server 独立的弹性策略（舱壁 + 熔断）。</summary>
    public McpPerServerResilienceOptions PerServer { get; set; } = new();

    /// <summary>单批工具调用的并行度上限（防 LLM 一回合吐出过多 tool_calls 时瞬时打爆）。</summary>
    [Range(1, 256)]
    public int MaxParallelToolCalls { get; set; } = 8;

    /// <summary>单次 <c>tools/call</c> 超时（秒）。Server 行 <c>timeout_ms&gt;0</c> 时按行覆盖。</summary>
    [Range(1, 600)]
    public int CallTimeoutSeconds { get; set; } = 60;

    /// <summary>建连 / 连通性测试（<c>initialize</c> 握手）超时（秒）。</summary>
    [Range(1, 120)]
    public int ConnectTimeoutSeconds { get; set; } = 10;
}

/// <summary>单个 MCP Server 的弹性策略参数（每 Server 一套熔断器与舱壁，互不影响）。</summary>
internal sealed class McpPerServerResilienceOptions
{
    /// <summary>并发许可上限（舱壁隔离），限制对单一 Server 的并发在途调用数。</summary>
    [Range(1, 10000)]
    public int MaxConcurrency { get; set; } = 8;

    /// <summary>并发队列上限。0 表示超出并发即快速失败、不排队。</summary>
    [Range(0, 10000)]
    public int QueueLimit { get; set; }

    /// <summary>熔断失败比例阈值（0..1）。采样窗口内失败率达到即开断路。</summary>
    [Range(0.1, 1.0)]
    public double CircuitFailureRatio { get; set; } = 0.5;

    /// <summary>熔断最小吞吐量。采样窗口内调用数低于此值不触发熔断。</summary>
    [Range(2, 1000)]
    public int CircuitMinimumThroughput { get; set; } = 5;

    /// <summary>熔断采样窗口（秒）。</summary>
    [Range(1, 600)]
    public int CircuitSamplingSeconds { get; set; } = 30;

    /// <summary>熔断打开后的隔离时长（秒），其间快速失败，到期后半开试探。</summary>
    [Range(1, 600)]
    public int CircuitBreakSeconds { get; set; } = 15;
}
