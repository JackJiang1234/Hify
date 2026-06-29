using Hify.Contracts.Mcp;
using Hify.Modules.Mcp.Features.Tools;
using Hify.Modules.Mcp.Persistence;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Mcp.Features.Invocation;

/// <summary>
/// <see cref="IMcpToolQuery"/> 实现：供 Conversation/Workflow 按工具 Id 取可调用工具元数据。
/// 仅返回 enabled &amp;&amp; available 的工具——停用或服务端已移除的不进 LLM 工具列表。
/// </summary>
internal sealed class McpToolQuery : IMcpToolQuery
{
    private readonly McpDbContext _db;

    public McpToolQuery(McpDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<McpToolDto>>> GetInvocableToolsAsync(
        IReadOnlyList<long> toolIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolIds);

        if (toolIds.Count == 0)
        {
            return Result<IReadOnlyList<McpToolDto>>.Ok([]);
        }

        var tools = await _db.McpTools.AsNoTracking()
            .Where(tool => toolIds.Contains(tool.Id) && tool.Enabled && tool.Available)
            .ToListAsync(cancellationToken);

        IReadOnlyList<McpToolDto> items = tools.Select(McpToolMapping.ToDto).ToList();
        return Result<IReadOnlyList<McpToolDto>>.Ok(items);
    }
}
