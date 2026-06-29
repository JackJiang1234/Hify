using System.Security.Cryptography;

using Hify.Modules.Mcp.Domain;
using Hify.Modules.Mcp.Features.Servers;
using Hify.Modules.Mcp.Persistence;
using Hify.Modules.Mcp.Protocol;
using Hify.Shared.Results;
using Hify.Shared.Security;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Hify.Modules.Mcp.Features.Tools;

/// <summary>
/// 工具发现：跑 <c>tools/list</c> 后按 (server_id, name) 原地 upsert——
/// 已存在则更新描述/Schema 且置 available=true（<b>id 不变</b>）；服务端已移除的仅置 available=false
/// （绝不软删、绝不换 id，以保护 agent_tool 对工具 id 的引用）；新出现的 INSERT。
/// 同步成功刷新 server 的 tool_count / last_synced_at / status；失败记 status=error。
/// </summary>
internal sealed class McpToolSyncService
{
    private const int LastErrorMaxLength = 512;

    private readonly McpDbContext _db;
    private readonly IMcpProtocolClient _protocolClient;
    private readonly ICredentialProtector _protector;
    private readonly IClock _clock;
    private readonly McpOptions _options;

    public McpToolSyncService(
        McpDbContext db,
        IMcpProtocolClient protocolClient,
        ICredentialProtector protector,
        IClock clock,
        IOptions<McpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(protocolClient);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);
        _db = db;
        _protocolClient = protocolClient;
        _protector = protector;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<Result<McpServerDto>> SyncToolsAsync(long serverId, CancellationToken cancellationToken)
    {
        var server = await _db.McpServers.FirstOrDefaultAsync(entity => entity.Id == serverId, cancellationToken);
        if (server is null)
        {
            return Result<McpServerDto>.Fail((int)McpErrorCode.McpServerNotFound, "MCP Server 不存在。");
        }

        string apiKey;
        try
        {
            apiKey = _protector.Unprotect(server.ApiKeyCipher);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return await RecordFailureAsync(server, "凭证解密失败", cancellationToken);
        }

        var connection = new McpServerConnection
        {
            Endpoint = server.Endpoint,
            AuthType = server.AuthType,
            AuthHeaderName = server.AuthHeaderName,
            ApiKey = apiKey,
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds));

        Result<IReadOnlyList<McpDiscoveredTool>> discovery;
        try
        {
            discovery = await _protocolClient.ListToolsAsync(connection, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await RecordFailureAsync(server, "工具发现超时", cancellationToken);
        }

        if (discovery.Code != 200)
        {
            return await RecordFailureAsync(server, discovery.Message, cancellationToken);
        }

        var discovered = discovery.Data!;
        await UpsertToolsAsync(server, discovered, cancellationToken);

        server.ToolCount = discovered.Count;
        server.LastSyncedAt = _clock.UtcNowEpochMs;
        server.Status = McpServerStatuses.Connected;
        server.LastError = string.Empty;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // 唯一索引 (server_id, name) 兜底并发同步插入冲突。
            return Result<McpServerDto>.Fail((int)McpErrorCode.McpProtocolError, "工具同步并发冲突，请重试。");
        }

        return Result<McpServerDto>.Ok(McpServerMapping.ToDto(server));
    }

    private async Task UpsertToolsAsync(McpServer server, IReadOnlyList<McpDiscoveredTool> discovered, CancellationToken cancellationToken)
    {
        var existing = await _db.McpTools
            .Where(tool => tool.ServerId == server.Id)
            .ToListAsync(cancellationToken);
        var existingByName = existing.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        var discoveredNames = new HashSet<string>(discovered.Select(tool => tool.Name), StringComparer.Ordinal);

        foreach (var tool in discovered)
        {
            if (existingByName.TryGetValue(tool.Name, out var row))
            {
                // 原地更新：id 保持不变。
                row.Description = tool.Description;
                row.InputSchema = tool.InputSchemaJson;
                row.Available = true;
            }
            else
            {
                _db.McpTools.Add(new McpTool
                {
                    ServerId = server.Id,
                    Name = tool.Name,
                    Description = tool.Description,
                    InputSchema = tool.InputSchemaJson,
                    Available = true,
                    Enabled = true,
                });
            }
        }

        // 服务端已移除的：仅置 available=false，绝不软删、绝不换 id。
        foreach (var row in existing.Where(tool => !discoveredNames.Contains(tool.Name)))
        {
            row.Available = false;
        }
    }

    private async Task<Result<McpServerDto>> RecordFailureAsync(McpServer server, string message, CancellationToken cancellationToken)
    {
        server.Status = McpServerStatuses.Error;
        server.LastError = Truncate(message, LastErrorMaxLength);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<McpServerDto>.Ok(McpServerMapping.ToDto(server));
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;
}
