using Hify.Contracts.Mcp;
using Hify.Contracts.ModelProvider;
using Hify.Modules.Mcp.Domain;
using Hify.Modules.Mcp.Features.Servers;
using Hify.Modules.Mcp.Persistence;
using Hify.Modules.Mcp.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Mcp.Tests.Features.Servers;

/// <summary>
/// MCP Server CRUD 的真实库测试（连不上则跳过）。用唯一名隔离，库为一次性验证库不清理残留。
/// </summary>
[Collection(McpDbCollection.Name)]
public sealed class McpServerServiceTests
{
    private readonly bool _available;

    public McpServerServiceTests(McpSchemaFixture fixture) => _available = fixture.Available;

    private static McpServerService NewService(McpDbContext db) => new(db, TestProtector.Create());

    private static CreateMcpServerRequest NewCreate(string name) => new()
    {
        Name = name,
        Endpoint = "https://mcp.test/mcp",
        AuthType = AuthTypes.Bearer,
        ApiKey = "sk-secret-123456",
        Enabled = true,
    };

    private static string UniqueName() => $"it-{Guid.NewGuid():N}";

    [Fact]
    public async Task CreateAsync_PersistsServer_EncryptsKey_DefaultsUnknownStatus()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var result = await NewService(db).CreateAsync(NewCreate(UniqueName()), CancellationToken.None);

        Assert.Equal(200, result.Code);
        var dto = result.Data!;
        Assert.Equal("…3456", dto.ApiKeyHint);
        Assert.Equal(McpServerStatuses.Unknown, dto.Status);
        Assert.Equal(McpTransports.StreamableHttp, dto.Transport);

        await using var verifyDb = TestDb.NewContext();
        var stored = await verifyDb.McpServers.AsNoTracking().FirstAsync(s => s.Id == dto.Id);
        Assert.NotEqual("sk-secret-123456", stored.ApiKeyCipher); // 落库为密文
        Assert.Equal("sk-secret-123456", TestProtector.Create().Unprotect(stored.ApiKeyCipher));
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ReturnsConflict()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var service = NewService(db);
        var name = UniqueName();
        await service.CreateAsync(NewCreate(name), CancellationToken.None);

        var second = await service.CreateAsync(NewCreate(name), CancellationToken.None);

        Assert.Equal((int)McpErrorCode.McpServerNameConflict, second.Code);
    }

    [Fact]
    public async Task GetAsync_Missing_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var result = await NewService(db).GetAsync(999_999_999, CancellationToken.None);

        Assert.Equal((int)McpErrorCode.McpServerNotFound, result.Code);
    }

    [Fact]
    public async Task ListAsync_ReturnsCreatedServer()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var service = NewService(db);
        var created = await service.CreateAsync(NewCreate(UniqueName()), CancellationToken.None);

        var page = await service.ListAsync(1, 20, CancellationToken.None);

        Assert.Equal(200, page.Code);
        Assert.True(page.Total >= 1);
        Assert.Contains(page.Data!, s => s.Id == created.Data!.Id);
    }

    [Fact]
    public async Task UpdateAsync_KeepsKeyWhenApiKeyEmpty_AndReplacesFields()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var service = NewService(db);
        var created = await service.CreateAsync(NewCreate(UniqueName()), CancellationToken.None);
        var id = created.Data!.Id;

        await using var readDb = TestDb.NewContext();
        var originalCipher = (await readDb.McpServers.AsNoTracking().FirstAsync(s => s.Id == id)).ApiKeyCipher;

        var update = new UpdateMcpServerRequest
        {
            Name = created.Data.Name,
            Endpoint = "https://changed.test/mcp",
            AuthType = AuthTypes.Bearer,
            ApiKey = string.Empty, // 留空保留原凭证
            Enabled = true,
        };
        var result = await service.UpdateAsync(id, update, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("https://changed.test/mcp", result.Data!.Endpoint);

        await using var verifyDb = TestDb.NewContext();
        var stored = await verifyDb.McpServers.AsNoTracking().FirstAsync(s => s.Id == id);
        Assert.Equal(originalCipher, stored.ApiKeyCipher); // 未改动
    }

    [Fact]
    public async Task UpdateAsync_ReencryptsWhenApiKeyProvided()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var service = NewService(db);
        var created = await service.CreateAsync(NewCreate(UniqueName()), CancellationToken.None);

        var update = new UpdateMcpServerRequest
        {
            Name = created.Data!.Name,
            Endpoint = created.Data.Endpoint,
            AuthType = AuthTypes.Bearer,
            ApiKey = "sk-rotated-9999",
            Enabled = true,
        };
        var result = await service.UpdateAsync(created.Data.Id, update, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("…9999", result.Data!.ApiKeyHint);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesServerAndTools()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var service = NewService(db);
        var created = await service.CreateAsync(NewCreate(UniqueName()), CancellationToken.None);
        var id = created.Data!.Id;

        // 直接插入一个该 Server 的工具，验证删除时级联软删。
        await using (var seedDb = TestDb.NewContext())
        {
            seedDb.McpTools.Add(new McpTool { ServerId = id, Name = "echo" });
            await seedDb.SaveChangesAsync(CancellationToken.None);
        }

        var deleted = await service.DeleteAsync(id, CancellationToken.None);
        Assert.Equal(200, deleted.Code);

        await using var verifyDb = TestDb.NewContext();
        Assert.Equal((int)McpErrorCode.McpServerNotFound, (await NewService(verifyDb).GetAsync(id, CancellationToken.None)).Code);
        Assert.Empty(await verifyDb.McpTools.AsNoTracking().Where(t => t.ServerId == id).ToListAsync());
    }

    [Fact]
    public async Task SetEnabledAsync_TogglesFlag()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var service = NewService(db);
        var created = await service.CreateAsync(NewCreate(UniqueName()), CancellationToken.None);
        var id = created.Data!.Id;

        await service.SetEnabledAsync(id, enabled: false, CancellationToken.None);

        await using var verifyDb = TestDb.NewContext();
        var dto = await NewService(verifyDb).GetAsync(id, CancellationToken.None);
        Assert.False(dto.Data!.Enabled);
    }
}
