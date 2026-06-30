using Hify.Contracts.ModelProvider;
using Hify.Modules.Mcp;
using Hify.Modules.Mcp.Domain;
using Hify.Modules.Mcp.Features.Invocation;
using Hify.Modules.Mcp.Features.Tools;
using Hify.Modules.Mcp.Persistence;
using Hify.Modules.Mcp.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Mcp.Tests.Features.Tools;

/// <summary>工具管理与只读查询：列表、启停、只读查询仅返回 enabled &amp;&amp; available。</summary>
[Collection(McpDbCollection.Name)]
public sealed class McpToolServiceTests
{
    private readonly bool _available;

    public McpToolServiceTests(McpSchemaFixture fixture) => _available = fixture.Available;

    private static async Task<long> SeedServerAsync(McpDbContext db)
    {
        var server = new McpServer
        {
            Name = $"it-{Guid.NewGuid():N}",
            Endpoint = "https://mcp.test/mcp",
            AuthType = AuthTypes.None,
        };
        db.McpServers.Add(server);
        await db.SaveChangesAsync(CancellationToken.None);
        return server.Id;
    }

    private static async Task<McpTool> SeedToolAsync(McpDbContext db, long serverId, string name, bool available, bool enabled)
    {
        var tool = new McpTool
        {
            ServerId = serverId,
            Name = name,
            Description = $"{name} desc",
            Available = available,
            Enabled = enabled,
        };
        db.McpTools.Add(tool);
        await db.SaveChangesAsync(CancellationToken.None);
        return tool;
    }

    [Fact]
    public async Task ListByServer_ReturnsAllTools_IncludingUnavailable()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var serverId = await SeedServerAsync(db);
        await SeedToolAsync(db, serverId, "alpha", available: true, enabled: true);
        await SeedToolAsync(db, serverId, "beta", available: false, enabled: true);

        var result = await new McpToolService(db).ListByServerAsync(serverId, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data!.Count); // 含 available=false 的
    }

    [Fact]
    public async Task ListByServer_MissingServer_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var result = await new McpToolService(db).ListByServerAsync(999_999_999, CancellationToken.None);

        Assert.Equal((int)McpErrorCode.McpServerNotFound, result.Code);
    }

    [Fact]
    public async Task SetToolEnabled_TogglesFlag()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var serverId = await SeedServerAsync(db);
        var tool = await SeedToolAsync(db, serverId, "alpha", available: true, enabled: true);

        var result = await new McpToolService(db).SetToolEnabledAsync(tool.Id, enabled: false, CancellationToken.None);
        Assert.Equal(200, result.Code);

        await using var verifyDb = TestDb.NewContext();
        var stored = await verifyDb.McpTools.AsNoTracking().FirstAsync(t => t.Id == tool.Id);
        Assert.False(stored.Enabled);
    }

    [Fact]
    public async Task SetToolEnabled_MissingTool_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var result = await new McpToolService(db).SetToolEnabledAsync(999_999_999, enabled: true, CancellationToken.None);

        Assert.Equal((int)McpErrorCode.McpToolNotFound, result.Code);
    }

    [Fact]
    public async Task GetInvocableTools_ReturnsOnlyEnabledAndAvailable()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var serverId = await SeedServerAsync(db);
        var ok = await SeedToolAsync(db, serverId, "ok", available: true, enabled: true);
        var disabled = await SeedToolAsync(db, serverId, "disabled", available: true, enabled: false);
        var unavailable = await SeedToolAsync(db, serverId, "unavailable", available: false, enabled: true);

        var query = new McpToolQuery(db);
        var result = await query.GetInvocableToolsAsync(
            [ok.Id, disabled.Id, unavailable.Id, 999_999_999], CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Single(result.Data!);
        Assert.Equal(ok.Id, result.Data![0].Id); // 仅启用且可用的进列表，未知 id 略过
    }

    [Fact]
    public async Task GetInvocableTools_EmptyInput_ReturnsEmpty()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var result = await new McpToolQuery(db).GetInvocableToolsAsync([], CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task PruneRemovedTools_SoftDeletesUnavailableOnly()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var serverId = await SeedServerAsync(db);
        var kept = await SeedToolAsync(db, serverId, "alive", available: true, enabled: true);
        await SeedToolAsync(db, serverId, "gone1", available: false, enabled: true);
        await SeedToolAsync(db, serverId, "gone2", available: false, enabled: false);

        var result = await new McpToolService(db).PruneRemovedToolsAsync(serverId, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data); // 清理两个不可用

        await using var verifyDb = TestDb.NewContext();
        var remaining = await verifyDb.McpTools.AsNoTracking().Where(t => t.ServerId == serverId).ToListAsync();
        Assert.Single(remaining); // 仅可用工具留存
        Assert.Equal(kept.Id, remaining[0].Id);
    }

    [Fact]
    public async Task PruneRemovedTools_MissingServer_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var result = await new McpToolService(db).PruneRemovedToolsAsync(999_999_999, CancellationToken.None);

        Assert.Equal((int)McpErrorCode.McpServerNotFound, result.Code);
    }
}
