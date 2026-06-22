using Hify.Contracts.Agent;
using Hify.Modules.Agent.Persistence;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Agent.Features.Agents;

/// <summary>
/// <see cref="IAgentQuery"/> 实现：供对话引擎只读装配 Agent 配置。自包含（仅依赖 DbContext），
/// 与 <see cref="AgentService"/>（CRUD）的写路径解耦，避免引入 IModelProviderQuery 等无关依赖。
/// </summary>
internal sealed class AgentQuery : IAgentQuery
{
    private readonly AgentDbContext _db;

    public AgentQuery(AgentDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Result<AgentDto>> GetAgentAsync(long agentId, CancellationToken cancellationToken)
    {
        var agent = await _db.Agents.AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == agentId, cancellationToken);
        if (agent is null)
        {
            return Result<AgentDto>.Fail((int)AgentErrorCode.AgentNotFound, "Agent 不存在。");
        }

        var toolIds = await _db.AgentTools.AsNoTracking()
            .Where(binding => binding.AgentId == agentId)
            .Select(binding => binding.ToolId)
            .ToListAsync(cancellationToken);
        var knowledgeBaseIds = await _db.AgentKnowledges.AsNoTracking()
            .Where(binding => binding.AgentId == agentId)
            .Select(binding => binding.KnowledgeBaseId)
            .ToListAsync(cancellationToken);

        return Result<AgentDto>.Ok(AgentMapping.ToDto(agent, toolIds, knowledgeBaseIds));
    }
}
