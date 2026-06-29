using Hify.Contracts.Mcp;
using Hify.Modules.Mcp.Domain;
using Hify.Modules.Mcp.Persistence;
using Hify.Shared.Pagination;
using Hify.Shared.Results;
using Hify.Shared.Security;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Mcp.Features.Servers;

/// <summary>MCP Server CRUD 应用服务。可预期失败返回 <see cref="Result{T}"/>（5xxx），不抛异常。</summary>
internal sealed class McpServerService
{
    private readonly McpDbContext _db;
    private readonly ICredentialProtector _protector;

    public McpServerService(McpDbContext db, ICredentialProtector protector)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(protector);
        _db = db;
        _protector = protector;
    }

    public async Task<Result<McpServerDto>> CreateAsync(CreateMcpServerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _db.McpServers.AnyAsync(server => server.Name == request.Name, cancellationToken))
        {
            return Result<McpServerDto>.Fail((int)McpErrorCode.McpServerNameConflict, "MCP Server 名称已存在。");
        }

        var server = new McpServer
        {
            Name = request.Name,
            Transport = McpTransports.StreamableHttp,
            Endpoint = request.Endpoint,
            AuthType = request.AuthType,
            AuthHeaderName = request.AuthHeaderName,
            ApiKeyCipher = _protector.Protect(request.ApiKey),
            ApiKeyHint = ApiKeyHint.Of(request.ApiKey),
            TimeoutMs = request.TimeoutMs,
            Enabled = request.Enabled,
        };

        try
        {
            _db.McpServers.Add(server);
            await _db.SaveChangesAsync(cancellationToken);
            return Result<McpServerDto>.Ok(McpServerMapping.ToDto(server));
        }
        catch (DbUpdateException)
        {
            // 唯一索引兜底并发重名。
            return Result<McpServerDto>.Fail((int)McpErrorCode.McpServerNameConflict, "MCP Server 名称已存在。");
        }
    }

    public async Task<Result<McpServerDto>> GetAsync(long id, CancellationToken cancellationToken)
    {
        var server = await _db.McpServers.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        return server is null
            ? Result<McpServerDto>.Fail((int)McpErrorCode.McpServerNotFound, "MCP Server 不存在。")
            : Result<McpServerDto>.Ok(McpServerMapping.ToDto(server));
    }

    public async Task<PageResult<McpServerDto>> ListAsync(int page, int size, CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Of(page, size);
        var query = _db.McpServers.AsNoTracking();

        var servers = await query.ApplyPage(pageRequest).ToListAsync(cancellationToken);
        var total = pageRequest.IsFirstPage ? await query.CountAsync(cancellationToken) : 0;

        var items = servers.Select(McpServerMapping.ToDto).ToList();
        return PageResult<McpServerDto>.Ok(items, total, pageRequest.Page, pageRequest.Size);
    }

    public async Task<Result<McpServerDto>> UpdateAsync(long id, UpdateMcpServerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var server = await _db.McpServers.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (server is null)
        {
            return Result<McpServerDto>.Fail((int)McpErrorCode.McpServerNotFound, "MCP Server 不存在。");
        }

        if (server.Name != request.Name
            && await _db.McpServers.AnyAsync(other => other.Name == request.Name && other.Id != id, cancellationToken))
        {
            return Result<McpServerDto>.Fail((int)McpErrorCode.McpServerNameConflict, "MCP Server 名称已存在。");
        }

        server.Name = request.Name;
        server.Endpoint = request.Endpoint;
        server.AuthType = request.AuthType;
        server.AuthHeaderName = request.AuthHeaderName;
        server.TimeoutMs = request.TimeoutMs;
        server.Enabled = request.Enabled;

        // 仅当提供了新凭证才重新加密；留空保留原凭证。
        if (request.ApiKey.Length > 0)
        {
            server.ApiKeyCipher = _protector.Protect(request.ApiKey);
            server.ApiKeyHint = ApiKeyHint.Of(request.ApiKey);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<McpServerDto>.Ok(McpServerMapping.ToDto(server));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var server = await _db.McpServers.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (server is null)
        {
            return Result<bool>.Fail((int)McpErrorCode.McpServerNotFound, "MCP Server 不存在。");
        }

        // 级联软删：Server + 其工具（SaveChanges 由 DbContext 转为软删）。
        var tools = await _db.McpTools.Where(tool => tool.ServerId == id).ToListAsync(cancellationToken);
        _db.McpTools.RemoveRange(tools);

        _db.McpServers.Remove(server);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> SetEnabledAsync(long id, bool enabled, CancellationToken cancellationToken)
    {
        var server = await _db.McpServers.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (server is null)
        {
            return Result<bool>.Fail((int)McpErrorCode.McpServerNotFound, "MCP Server 不存在。");
        }

        server.Enabled = enabled;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }
}
