using System.Security.Cryptography;

using Hify.Modules.Mcp.Persistence;
using Hify.Modules.Mcp.Protocol;
using Hify.Shared.Results;
using Hify.Shared.Security;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Hify.Modules.Mcp.Features.Servers;

/// <summary>
/// MCP Server 连通性测试：解密凭证 → 跑 initialize 握手 → 把结果写入 server.status / last_error。
/// 除「Server 不存在」外一律返回 Ok(Server 快照)（握手/解密失败记为 error 状态），供「测试」按钮始终拿到状态。
/// </summary>
internal sealed class McpConnectivityService
{
    private const int LastErrorMaxLength = 512;

    private readonly McpDbContext _db;
    private readonly IMcpProtocolClient _protocolClient;
    private readonly ICredentialProtector _protector;
    private readonly McpOptions _options;

    public McpConnectivityService(
        McpDbContext db,
        IMcpProtocolClient protocolClient,
        ICredentialProtector protector,
        IOptions<McpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(protocolClient);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(options);
        _db = db;
        _protocolClient = protocolClient;
        _protector = protector;
        _options = options.Value;
    }

    public async Task<Result<McpServerDto>> TestConnectionAsync(long serverId, CancellationToken cancellationToken)
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

        // 连接超时由本服务用链路取消控制；协议客户端在其 token 被取消时会抛出 OCE，故在此区分超时与调用方取消。
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds));

        Result<McpServerDescriptor> handshake;
        try
        {
            handshake = await _protocolClient.InitializeAsync(connection, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await RecordFailureAsync(server, "连接超时", cancellationToken);
        }

        if (handshake.Code != 200)
        {
            return await RecordFailureAsync(server, handshake.Message, cancellationToken);
        }

        server.Status = McpServerStatuses.Connected;
        server.LastError = string.Empty;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<McpServerDto>.Ok(McpServerMapping.ToDto(server));
    }

    private async Task<Result<McpServerDto>> RecordFailureAsync(Domain.McpServer server, string message, CancellationToken cancellationToken)
    {
        server.Status = McpServerStatuses.Error;
        server.LastError = Truncate(message, LastErrorMaxLength);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<McpServerDto>.Ok(McpServerMapping.ToDto(server));
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;
}
