using Hify.Contracts.Mcp;
using Hify.Contracts.ModelProvider;
using Hify.Modules.Mcp;
using Hify.Modules.Mcp.Domain;
using Hify.Modules.Mcp.Features.Servers;
using Hify.Modules.Mcp.Persistence;
using Hify.Modules.Mcp.Protocol;
using Hify.Modules.Mcp.Tests.Support;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Hify.Modules.Mcp.Tests.Features.Servers;

/// <summary>连通性测试服务：握手成功置 connected、失败/超时/解密失败置 error 并记错误，不存在返回 NotFound。</summary>
[Collection(McpDbCollection.Name)]
public sealed class McpConnectivityServiceTests
{
    private readonly bool _available;

    public McpConnectivityServiceTests(McpSchemaFixture fixture) => _available = fixture.Available;

    private static McpConnectivityService NewService(McpDbContext db, FakeProtocolClient client) =>
        new(db, client, TestProtector.Create(), Options.Create(new McpOptions()));

    private static async Task<long> SeedServerAsync(McpDbContext db)
    {
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

    [Fact]
    public async Task TestConnection_HandshakeSucceeds_SetsConnected()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var id = await SeedServerAsync(db);
        var client = new FakeProtocolClient
        {
            InitializeHandler = (_, _) => Task.FromResult(Result<McpServerDescriptor>.Ok(new McpServerDescriptor { Name = "srv", Version = "1.0" })),
        };

        var result = await NewService(db, client).TestConnectionAsync(id, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(McpServerStatuses.Connected, result.Data!.Status);
        Assert.Equal(string.Empty, result.Data.LastError);
    }

    [Fact]
    public async Task TestConnection_HandshakeFails_SetsErrorWithMessage()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var id = await SeedServerAsync(db);
        var client = new FakeProtocolClient
        {
            InitializeHandler = (_, _) => Task.FromResult(Result<McpServerDescriptor>.Fail((int)McpErrorCode.McpServerUnreachable, "连接被拒")),
        };

        var result = await NewService(db, client).TestConnectionAsync(id, CancellationToken.None);

        Assert.Equal(200, result.Code); // 仍返回 Ok(快照)
        Assert.Equal(McpServerStatuses.Error, result.Data!.Status);
        Assert.Contains("连接被拒", result.Data.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestConnection_MissingServer_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var result = await NewService(db, new FakeProtocolClient()).TestConnectionAsync(999_999_999, CancellationToken.None);

        Assert.Equal((int)McpErrorCode.McpServerNotFound, result.Code);
    }

    [Fact]
    public async Task TestConnection_CorruptedCredential_SetsError()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var server = new McpServer
        {
            Name = $"it-{Guid.NewGuid():N}",
            Transport = McpTransports.StreamableHttp,
            Endpoint = "https://mcp.test/mcp",
            AuthType = AuthTypes.Bearer,
            ApiKeyCipher = "not-valid-base64!!!", // 解密时抛 FormatException
        };
        db.McpServers.Add(server);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await NewService(db, new FakeProtocolClient()).TestConnectionAsync(server.Id, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(McpServerStatuses.Error, result.Data!.Status);
        Assert.Contains("凭证解密", result.Data.LastError, StringComparison.Ordinal);
    }
}
