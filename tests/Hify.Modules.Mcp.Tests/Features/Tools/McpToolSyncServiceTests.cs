using Hify.Contracts.Mcp;
using Hify.Contracts.ModelProvider;
using Hify.Modules.Mcp;
using Hify.Modules.Mcp.Domain;
using Hify.Modules.Mcp.Features.Servers;
using Hify.Modules.Mcp.Features.Tools;
using Hify.Modules.Mcp.Persistence;
using Hify.Modules.Mcp.Protocol;
using Hify.Modules.Mcp.Tests.Support;
using Hify.Shared.Results;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Hify.Modules.Mcp.Tests.Features.Tools;

/// <summary>
/// 工具发现 upsert：首次插入、原地更新、消失仅标记 unavailable、重现复用同 id（核心：id 稳定保护 agent 绑定）。
/// </summary>
[Collection(McpDbCollection.Name)]
public sealed class McpToolSyncServiceTests
{
    private readonly bool _available;

    public McpToolSyncServiceTests(McpSchemaFixture fixture) => _available = fixture.Available;

    private static McpToolSyncService NewService(McpDbContext db, FakeProtocolClient client) =>
        new(db, client, TestProtector.Create(), new SystemClock(), Options.Create(new McpOptions()));

    private static FakeProtocolClient Listing(params string[] names)
    {
        IReadOnlyList<McpDiscoveredTool> tools = names
            .Select(name => new McpDiscoveredTool { Name = name, Description = $"{name} desc", InputSchemaJson = "{}" })
            .ToList();
        return new FakeProtocolClient
        {
            ListToolsHandler = (_, _) => Task.FromResult(Result<IReadOnlyList<McpDiscoveredTool>>.Ok(tools)),
        };
    }

    private static async Task<long> SeedServerAsync()
    {
        await using var db = TestDb.NewContext();
        var server = new McpServer
        {
            Name = $"it-{Guid.NewGuid():N}",
            Transport = McpTransports.StreamableHttp,
            Endpoint = "https://mcp.test/mcp",
            AuthType = AuthTypes.None,
        };
        db.McpServers.Add(server);
        await db.SaveChangesAsync(CancellationToken.None);
        return server.Id;
    }

    private static async Task SyncAsync(long serverId, params string[] toolNames)
    {
        await using var db = TestDb.NewContext();
        var result = await NewService(db, Listing(toolNames)).SyncToolsAsync(serverId, CancellationToken.None);
        Assert.Equal(200, result.Code);
    }

    private static async Task<IReadOnlyList<McpTool>> LoadToolsAsync(long serverId)
    {
        await using var db = TestDb.NewContext();
        return await db.McpTools.AsNoTracking().Where(tool => tool.ServerId == serverId).ToListAsync();
    }

    [Fact]
    public async Task FirstSync_InsertsTools_RefreshesServerMetadata()
    {
        if (!_available)
        {
            return;
        }

        var id = await SeedServerAsync();

        await using var db = TestDb.NewContext();
        var result = await NewService(db, Listing("alpha", "beta")).SyncToolsAsync(id, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data!.ToolCount);
        Assert.Equal(McpServerStatuses.Connected, result.Data.Status);
        Assert.True(result.Data.LastSyncedAt > 0);

        var tools = await LoadToolsAsync(id);
        Assert.Equal(2, tools.Count);
        Assert.All(tools, tool => Assert.True(tool.Available));
    }

    [Fact]
    public async Task ReSync_DisappearedTool_KeptUnavailable_WithStableId()
    {
        if (!_available)
        {
            return;
        }

        var id = await SeedServerAsync();
        await SyncAsync(id, "alpha", "beta");

        var before = await LoadToolsAsync(id);
        var alphaId = before.Single(tool => tool.Name == "alpha").Id;
        var betaId = before.Single(tool => tool.Name == "beta").Id;

        await SyncAsync(id, "alpha"); // beta 消失

        var after = await LoadToolsAsync(id);
        var alpha = after.Single(tool => tool.Name == "alpha");
        var beta = after.Single(tool => tool.Name == "beta"); // 行仍在（未软删）

        Assert.Equal(alphaId, alpha.Id);
        Assert.True(alpha.Available);
        Assert.Equal(betaId, beta.Id);   // 同 id，未换行
        Assert.False(beta.Available);    // 仅标记不可用
    }

    [Fact]
    public async Task ReSync_ReappearedTool_ReusesSameId()
    {
        if (!_available)
        {
            return;
        }

        var id = await SeedServerAsync();
        await SyncAsync(id, "alpha", "beta");
        var betaId = (await LoadToolsAsync(id)).Single(tool => tool.Name == "beta").Id;

        await SyncAsync(id, "alpha");            // beta 消失 → available=false
        await SyncAsync(id, "alpha", "beta");    // beta 重现

        var beta = (await LoadToolsAsync(id)).Single(tool => tool.Name == "beta");
        Assert.Equal(betaId, beta.Id);  // 重现复用同一 id，agent 绑定不断
        Assert.True(beta.Available);
    }

    [Fact]
    public async Task ReSync_UpdatesDescriptionAndSchema_InPlace()
    {
        if (!_available)
        {
            return;
        }

        var id = await SeedServerAsync();
        await SyncAsync(id, "alpha");
        var alphaId = (await LoadToolsAsync(id)).Single(tool => tool.Name == "alpha").Id;

        // 用不同描述/Schema 再次发现同名工具。
        await using (var db = TestDb.NewContext())
        {
            var client = new FakeProtocolClient
            {
                ListToolsHandler = (_, _) => Task.FromResult(Result<IReadOnlyList<McpDiscoveredTool>>.Ok(
                    [new McpDiscoveredTool { Name = "alpha", Description = "updated", InputSchemaJson = """{"type":"object"}""" }])),
            };
            await NewService(db, client).SyncToolsAsync(id, CancellationToken.None);
        }

        var alpha = (await LoadToolsAsync(id)).Single(tool => tool.Name == "alpha");
        Assert.Equal(alphaId, alpha.Id); // id 不变
        Assert.Equal("updated", alpha.Description);
        Assert.Contains("object", alpha.InputSchema, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sync_ListFails_SetsErrorStatus_LeavesToolsUntouched()
    {
        if (!_available)
        {
            return;
        }

        var id = await SeedServerAsync();
        await SyncAsync(id, "alpha");

        await using var db = TestDb.NewContext();
        var failing = new FakeProtocolClient
        {
            ListToolsHandler = (_, _) => Task.FromResult(Result<IReadOnlyList<McpDiscoveredTool>>.Fail((int)McpErrorCode.McpServerUnreachable, "断开")),
        };
        var result = await NewService(db, failing).SyncToolsAsync(id, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(McpServerStatuses.Error, result.Data!.Status);
        Assert.Contains("断开", result.Data.LastError, StringComparison.Ordinal);

        var tools = await LoadToolsAsync(id);
        Assert.Single(tools); // 失败不动既有工具
        Assert.True(tools[0].Available);
    }

    [Fact]
    public async Task Sync_MissingServer_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var result = await NewService(db, Listing("alpha")).SyncToolsAsync(999_999_999, CancellationToken.None);

        Assert.Equal((int)McpErrorCode.McpServerNotFound, result.Code);
    }
}
