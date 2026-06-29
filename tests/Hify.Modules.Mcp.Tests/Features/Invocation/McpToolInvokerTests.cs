using Hify.Contracts.Mcp;
using Hify.Contracts.ModelProvider;
using Hify.Modules.Mcp;
using Hify.Modules.Mcp.Domain;
using Hify.Modules.Mcp.Features.Invocation;
using Hify.Modules.Mcp.Persistence;
using Hify.Modules.Mcp.Protocol;
using Hify.Modules.Mcp.Tests.Support;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Hify.Modules.Mcp.Tests.Features.Invocation;

/// <summary>
/// 工具调用：解析失败映射、成功、工具级 isError、服务端失败；批量并发的部分失败隔离 / 顺序一致 / 真并行。
/// </summary>
[Collection(McpDbCollection.Name)]
public sealed class McpToolInvokerTests
{
    private readonly bool _available;

    public McpToolInvokerTests(McpSchemaFixture fixture) => _available = fixture.Available;

    private static McpToolInvoker NewInvoker(McpDbContext db, FakeProtocolClient client, McpResiliencePipelineProvider pipelines) =>
        new(db, client, TestProtector.Create(), pipelines, Options.Create(new McpOptions()));

    private static async Task<long> SeedServerAsync(McpDbContext db, bool enabled = true)
    {
        var server = new McpServer
        {
            Name = $"it-{Guid.NewGuid():N}",
            Endpoint = "https://mcp.test/mcp",
            AuthType = AuthTypes.None,
            Enabled = enabled,
        };
        db.McpServers.Add(server);
        await db.SaveChangesAsync(CancellationToken.None);
        return server.Id;
    }

    private static async Task<long> SeedToolAsync(McpDbContext db, long serverId, bool enabled = true, bool available = true)
    {
        var tool = new McpTool
        {
            ServerId = serverId,
            Name = $"tool-{Guid.NewGuid():N}",
            Enabled = enabled,
            Available = available,
        };
        db.McpTools.Add(tool);
        await db.SaveChangesAsync(CancellationToken.None);
        return tool.Id;
    }

    private static FakeProtocolClient Returning(Result<McpToolResult> result) =>
        new() { CallToolHandler = (_, _, _, _) => Task.FromResult(result) };

    [Fact]
    public async Task InvokeAsync_Success_ReturnsContent()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var serverId = await SeedServerAsync(db);
        var toolId = await SeedToolAsync(db, serverId);
        using var pipelines = new McpResiliencePipelineProvider(Options.Create(new McpOptions()));
        var client = Returning(Result<McpToolResult>.Ok(new McpToolResult { Content = "hi" }));

        var result = await NewInvoker(db, client, pipelines)
            .InvokeAsync(new McpToolCall { CallId = "c1", ToolId = toolId }, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("hi", result.Data!.Content);
        Assert.False(result.Data.IsError);
    }

    [Fact]
    public async Task InvokeAsync_ToolLevelError_ReturnsOkWithIsError()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var serverId = await SeedServerAsync(db);
        var toolId = await SeedToolAsync(db, serverId);
        using var pipelines = new McpResiliencePipelineProvider(Options.Create(new McpOptions()));
        var client = Returning(Result<McpToolResult>.Ok(new McpToolResult { Content = "tool failed", IsError = true }));

        var result = await NewInvoker(db, client, pipelines)
            .InvokeAsync(new McpToolCall { CallId = "c1", ToolId = toolId }, CancellationToken.None);

        Assert.Equal(200, result.Code); // 调用成功
        Assert.True(result.Data!.IsError); // 但工具级报错
    }

    [Fact]
    public async Task InvokeAsync_ServerLevelFailure_ReturnsFailure()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var serverId = await SeedServerAsync(db);
        var toolId = await SeedToolAsync(db, serverId);
        using var pipelines = new McpResiliencePipelineProvider(Options.Create(new McpOptions()));
        var client = Returning(Result<McpToolResult>.Fail((int)McpErrorCode.McpServerUnreachable, "断开"));

        var result = await NewInvoker(db, client, pipelines)
            .InvokeAsync(new McpToolCall { CallId = "c1", ToolId = toolId }, CancellationToken.None);

        Assert.Equal((int)McpErrorCode.McpServerUnreachable, result.Code);
    }

    [Theory]
    [InlineData(false, true, true, (int)McpErrorCode.McpToolUnavailable)]   // 工具停用
    [InlineData(true, false, true, (int)McpErrorCode.McpToolUnavailable)]   // 工具不可用
    [InlineData(true, true, false, (int)McpErrorCode.McpServerDisabled)]    // Server 停用
    public async Task InvokeAsync_ResolutionFailures_MapToCodes(bool toolEnabled, bool toolAvailable, bool serverEnabled, int expectedCode)
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var serverId = await SeedServerAsync(db, enabled: serverEnabled);
        var toolId = await SeedToolAsync(db, serverId, enabled: toolEnabled, available: toolAvailable);
        using var pipelines = new McpResiliencePipelineProvider(Options.Create(new McpOptions()));

        var result = await NewInvoker(db, Returning(Result<McpToolResult>.Ok(new McpToolResult())), pipelines)
            .InvokeAsync(new McpToolCall { CallId = "c1", ToolId = toolId }, CancellationToken.None);

        Assert.Equal(expectedCode, result.Code);
    }

    [Fact]
    public async Task InvokeAsync_ToolNotFound_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        using var pipelines = new McpResiliencePipelineProvider(Options.Create(new McpOptions()));

        var result = await NewInvoker(db, Returning(Result<McpToolResult>.Ok(new McpToolResult())), pipelines)
            .InvokeAsync(new McpToolCall { CallId = "c1", ToolId = 999_999_999 }, CancellationToken.None);

        Assert.Equal((int)McpErrorCode.McpToolNotFound, result.Code);
    }

    [Fact]
    public async Task InvokeManyAsync_IsolatesPartialFailure_PreservesOrder()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var serverId = await SeedServerAsync(db);
        var okTool = await SeedToolAsync(db, serverId);
        var disabledTool = await SeedToolAsync(db, serverId, enabled: false);
        using var pipelines = new McpResiliencePipelineProvider(Options.Create(new McpOptions()));
        var client = Returning(Result<McpToolResult>.Ok(new McpToolResult { Content = "ok" }));

        var calls = new List<McpToolCall>
        {
            new() { CallId = "first", ToolId = okTool },
            new() { CallId = "second", ToolId = disabledTool },
        };
        var results = await NewInvoker(db, client, pipelines).InvokeManyAsync(calls, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal("first", results[0].CallId); // 顺序与入参一致
        Assert.Equal("second", results[1].CallId);
        Assert.Equal(200, results[0].Result.Code); // 成功不受失败项影响
        Assert.Equal((int)McpErrorCode.McpToolUnavailable, results[1].Result.Code);
    }

    [Fact]
    public async Task InvokeManyAsync_RunsCallsConcurrently()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var serverId = await SeedServerAsync(db);
        var calls = new List<McpToolCall>();
        for (var i = 0; i < 4; i++)
        {
            calls.Add(new McpToolCall { CallId = i.ToString(), ToolId = await SeedToolAsync(db, serverId) });
        }

        using var pipelines = new McpResiliencePipelineProvider(Options.Create(new McpOptions()));
        var current = 0;
        var peak = 0;
        var sync = new object();
        var client = new FakeProtocolClient
        {
            CallToolHandler = async (_, _, _, ct) =>
            {
                lock (sync)
                {
                    current++;
                    peak = Math.Max(peak, current);
                }

                await Task.Delay(100, ct);
                lock (sync)
                {
                    current--;
                }

                return Result<McpToolResult>.Ok(new McpToolResult { Content = "ok" });
            },
        };

        var results = await NewInvoker(db, client, pipelines).InvokeManyAsync(calls, CancellationToken.None);

        Assert.Equal(4, results.Count);
        Assert.All(results, invocation => Assert.Equal(200, invocation.Result.Code));
        Assert.True(peak >= 2, $"应并发执行，实测峰值并发 {peak}"); // 证明并行（非串行）
    }
}
