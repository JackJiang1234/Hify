using Hify.Contracts.Mcp;
using Hify.Modules.Mcp.Persistence;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Mcp.Features.Tools;

/// <summary>工具管理：列出某 Server 的工具（含 available/enabled）、启停单个工具。</summary>
internal sealed class McpToolService
{
    private readonly McpDbContext _db;

    public McpToolService(McpDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<IReadOnlyList<McpToolDto>>> ListByServerAsync(long serverId, CancellationToken cancellationToken)
    {
        if (!await _db.McpServers.AnyAsync(server => server.Id == serverId, cancellationToken))
        {
            return Result<IReadOnlyList<McpToolDto>>.Fail((int)McpErrorCode.McpServerNotFound, "MCP Server 不存在。");
        }

        var tools = await _db.McpTools.AsNoTracking()
            .Where(tool => tool.ServerId == serverId)
            .OrderBy(tool => tool.Name)
            .ToListAsync(cancellationToken);

        IReadOnlyList<McpToolDto> items = tools.Select(McpToolMapping.ToDto).ToList();
        return Result<IReadOnlyList<McpToolDto>>.Ok(items);
    }

    public async Task<Result<bool>> SetToolEnabledAsync(long toolId, bool enabled, CancellationToken cancellationToken)
    {
        var tool = await _db.McpTools.FirstOrDefaultAsync(entity => entity.Id == toolId, cancellationToken);
        if (tool is null)
        {
            return Result<bool>.Fail((int)McpErrorCode.McpToolNotFound, "工具不存在。");
        }

        tool.Enabled = enabled;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }
}
