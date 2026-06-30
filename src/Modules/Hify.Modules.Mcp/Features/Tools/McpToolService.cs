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

    /// <summary>
    /// 清理该 Server 下「服务端已移除」（available=false）的工具：软删之，返回清理数量。
    /// 管理员主动动作——若有 agent_tool 绑定指向被清理工具，将成为悬挂引用，由调用方（invoker）按
    /// <c>McpToolNotFound</c> 优雅降级（软删后全局过滤不可见）。仅清理 available=false，可用工具不动（清了也会被下次同步重建）。
    /// </summary>
    public async Task<Result<int>> PruneRemovedToolsAsync(long serverId, CancellationToken cancellationToken)
    {
        if (!await _db.McpServers.AnyAsync(server => server.Id == serverId, cancellationToken))
        {
            return Result<int>.Fail((int)McpErrorCode.McpServerNotFound, "MCP Server 不存在。");
        }

        var removed = await _db.McpTools
            .Where(tool => tool.ServerId == serverId && !tool.Available)
            .ToListAsync(cancellationToken);
        if (removed.Count == 0)
        {
            return Result<int>.Ok(0);
        }

        _db.McpTools.RemoveRange(removed); // SaveChanges 由 DbContext 转为软删
        await _db.SaveChangesAsync(cancellationToken);
        return Result<int>.Ok(removed.Count);
    }
}
