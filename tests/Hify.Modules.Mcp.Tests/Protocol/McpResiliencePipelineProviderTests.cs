using Hify.Modules.Mcp;
using Hify.Modules.Mcp.Protocol;

using Microsoft.Extensions.Options;

using Polly;
using Polly.CircuitBreaker;

namespace Hify.Modules.Mcp.Tests.Protocol;

/// <summary>每-Server 弹性管道：同 Server 复用同一管道、不同 Server 隔离、熔断确实生效。</summary>
public sealed class McpResiliencePipelineProviderTests
{
    private static McpResiliencePipelineProvider CreateProvider() =>
        new(Options.Create(new McpOptions()));

    [Fact]
    public void GetPipeline_SameServerId_ReturnsSameInstance()
    {
        using var provider = CreateProvider();

        var first = provider.GetPipeline(1);
        var second = provider.GetPipeline(1);

        Assert.Same(first, second); // 同 Server 的熔断/舱壁状态须跨调用累积
    }

    [Fact]
    public void GetPipeline_DifferentServerId_ReturnsDifferentInstances()
    {
        using var provider = CreateProvider();

        var first = provider.GetPipeline(1);
        var second = provider.GetPipeline(2);

        Assert.NotSame(first, second); // 一个 Server 故障不应波及另一个
    }

    [Fact]
    public async Task Pipeline_OpensCircuit_AfterRepeatedFailures()
    {
        using var provider = CreateProvider();
        var pipeline = provider.GetPipeline(42);

        var tripped = false;
        for (var attempt = 0; attempt < 50 && !tripped; attempt++)
        {
            try
            {
                await pipeline.ExecuteAsync(_ => throw new InvalidOperationException("boom"));
            }
            catch (BrokenCircuitException)
            {
                tripped = true; // 断路已打开 → 后续快速失败
            }
            catch (InvalidOperationException)
            {
                // 断路打开前的真实失败，继续累积。
            }
        }

        Assert.True(tripped, "连续失败后断路器应打开（验证熔断已挂载）");
    }
}
