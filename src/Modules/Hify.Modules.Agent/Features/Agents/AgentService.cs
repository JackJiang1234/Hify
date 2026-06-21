using Hify.Contracts.Agent;
using Hify.Contracts.ModelProvider;
using Hify.Modules.Agent.Domain;
using Hify.Modules.Agent.Persistence;
using Hify.Shared.Pagination;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Agent.Features.Agents;

/// <summary>
/// Agent 配置 CRUD 应用服务。可预期失败返回 <see cref="Result{T}"/>（3xxx），不抛异常。
/// 引用校验（方案 B）：仅经 <see cref="IModelProviderQuery"/>（L0）校验模型；工具/知识库仅存 ID，
/// 其存在性由对话引擎（L2）运行时校验，避免 Agent 横向依赖同层 Knowledge 模块。
/// </summary>
internal sealed class AgentService
{
    private readonly AgentDbContext _db;
    private readonly IModelProviderQuery _models;

    public AgentService(AgentDbContext db, IModelProviderQuery models)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(models);
        _db = db;
        _models = models;
    }

    public async Task<Result<AgentDto>> CreateAsync(CreateAgentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _db.Agents.AnyAsync(agent => agent.Name == request.Name, cancellationToken))
        {
            return Result<AgentDto>.Fail((int)AgentErrorCode.AgentNameConflict, "Agent 名称已存在。");
        }

        var modelError = await ValidateModelAsync(request.ModelId, request.ToolIds.Count > 0, request.ModelParams, cancellationToken);
        if (modelError is not null)
        {
            return modelError;
        }

        var agent = new Domain.Agent
        {
            Name = request.Name,
            Description = request.Description,
            ModelId = request.ModelId,
            SystemPrompt = request.SystemPrompt,
            ModelParams = AgentParamsJson.SerializeModelParams(request.ModelParams),
            RetrievalParams = AgentParamsJson.SerializeRetrievalParams(request.RetrievalParams),
            MaxIterations = request.MaxIterations,
            Enabled = request.Enabled,
        };

        // 同事务建主表 + 绑定行（绑定需主表生成的 Id）。
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.Agents.Add(agent);
            await _db.SaveChangesAsync(cancellationToken);

            AddBindings(agent.Id, request.ToolIds, request.KnowledgeBaseIds);
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<AgentDto>.Fail((int)AgentErrorCode.AgentNameConflict, "Agent 名称已存在。");
        }

        var (toolIds, knowledgeBaseIds) = await LoadBindingsAsync(agent.Id, cancellationToken);
        return Result<AgentDto>.Ok(AgentMapping.ToDto(agent, toolIds, knowledgeBaseIds));
    }

    public async Task<Result<AgentDto>> GetAsync(long id, CancellationToken cancellationToken)
    {
        var agent = await _db.Agents.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (agent is null)
        {
            return Result<AgentDto>.Fail((int)AgentErrorCode.AgentNotFound, "Agent 不存在。");
        }

        var (toolIds, knowledgeBaseIds) = await LoadBindingsAsync(id, cancellationToken);
        return Result<AgentDto>.Ok(AgentMapping.ToDto(agent, toolIds, knowledgeBaseIds));
    }

    public async Task<PageResult<AgentDto>> ListAsync(int page, int size, CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Of(page, size);
        var query = _db.Agents.AsNoTracking();

        var agents = await query.ApplyPage(pageRequest).ToListAsync(cancellationToken);
        var total = pageRequest.IsFirstPage ? await query.CountAsync(cancellationToken) : 0;

        var ids = agents.Select(agent => agent.Id).ToList();

        var toolsByAgent = (await _db.AgentTools.AsNoTracking()
                .Where(binding => ids.Contains(binding.AgentId))
                .Select(binding => new { binding.AgentId, binding.ToolId })
                .ToListAsync(cancellationToken))
            .GroupBy(row => row.AgentId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<long>)group.Select(row => row.ToolId).ToList());

        var knowledgeByAgent = (await _db.AgentKnowledges.AsNoTracking()
                .Where(binding => ids.Contains(binding.AgentId))
                .Select(binding => new { binding.AgentId, binding.KnowledgeBaseId })
                .ToListAsync(cancellationToken))
            .GroupBy(row => row.AgentId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<long>)group.Select(row => row.KnowledgeBaseId).ToList());

        var items = agents
            .Select(agent => AgentMapping.ToDto(
                agent,
                toolsByAgent.GetValueOrDefault(agent.Id, []),
                knowledgeByAgent.GetValueOrDefault(agent.Id, [])))
            .ToList();

        return PageResult<AgentDto>.Ok(items, total, pageRequest.Page, pageRequest.Size);
    }

    public async Task<Result<AgentDto>> UpdateAsync(long id, UpdateAgentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agent = await _db.Agents.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (agent is null)
        {
            return Result<AgentDto>.Fail((int)AgentErrorCode.AgentNotFound, "Agent 不存在。");
        }

        if (agent.Name != request.Name
            && await _db.Agents.AnyAsync(other => other.Name == request.Name && other.Id != id, cancellationToken))
        {
            return Result<AgentDto>.Fail((int)AgentErrorCode.AgentNameConflict, "Agent 名称已存在。");
        }

        var modelError = await ValidateModelAsync(request.ModelId, request.ToolIds.Count > 0, request.ModelParams, cancellationToken);
        if (modelError is not null)
        {
            return modelError;
        }

        agent.Name = request.Name;
        agent.Description = request.Description;
        agent.ModelId = request.ModelId;
        agent.SystemPrompt = request.SystemPrompt;
        agent.ModelParams = AgentParamsJson.SerializeModelParams(request.ModelParams);
        agent.RetrievalParams = AgentParamsJson.SerializeRetrievalParams(request.RetrievalParams);
        agent.MaxIterations = request.MaxIterations;
        agent.Enabled = request.Enabled;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await ReplaceBindingsAsync(id, request.ToolIds, request.KnowledgeBaseIds, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<AgentDto>.Fail((int)AgentErrorCode.AgentNameConflict, "Agent 名称已存在。");
        }

        var (toolIds, knowledgeBaseIds) = await LoadBindingsAsync(id, cancellationToken);
        return Result<AgentDto>.Ok(AgentMapping.ToDto(agent, toolIds, knowledgeBaseIds));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (agent is null)
        {
            return Result<bool>.Fail((int)AgentErrorCode.AgentNotFound, "Agent 不存在。");
        }

        // 级联软删：主表 + 工具/知识库绑定（SaveChanges 由 DbContext 转为软删）。
        var tools = await _db.AgentTools.Where(binding => binding.AgentId == id).ToListAsync(cancellationToken);
        _db.AgentTools.RemoveRange(tools);
        var knowledge = await _db.AgentKnowledges.Where(binding => binding.AgentId == id).ToListAsync(cancellationToken);
        _db.AgentKnowledges.RemoveRange(knowledge);
        _db.Agents.Remove(agent);

        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> SetEnabledAsync(long id, bool enabled, CancellationToken cancellationToken)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (agent is null)
        {
            return Result<bool>.Fail((int)AgentErrorCode.AgentNotFound, "Agent 不存在。");
        }

        agent.Enabled = enabled;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }

    // 方案 B：模型是 Agent 唯一会校验存在性的引用（ModelProvider 为 L0，依赖合法）。
    private async Task<Result<AgentDto>?> ValidateModelAsync(long modelId, bool hasTools, ModelParams? modelParams, CancellationToken cancellationToken)
    {
        var result = await _models.GetModelAsync(modelId, cancellationToken);
        if (result.Code != 200 || result.Data is null)
        {
            return Result<AgentDto>.Fail((int)AgentErrorCode.AgentModelInvalid, "引用的模型不存在。");
        }

        var model = result.Data;
        if (model.ModelType != ModelTypes.Chat)
        {
            return Result<AgentDto>.Fail((int)AgentErrorCode.AgentModelInvalid, "引用的模型不是 chat 类型。");
        }

        if (!model.Enabled)
        {
            return Result<AgentDto>.Fail((int)AgentErrorCode.AgentModelInvalid, "引用的模型已停用。");
        }

        if (hasTools && !model.SupportsTools)
        {
            return Result<AgentDto>.Fail((int)AgentErrorCode.ModelToolUnsupported, "所选模型不支持工具调用，无法绑定工具。");
        }

        if (modelParams?.MaxTokens is int maxTokens && model.MaxOutputTokens > 0 && maxTokens > model.MaxOutputTokens)
        {
            return Result<AgentDto>.Fail((int)AgentErrorCode.AgentModelInvalid, $"maxTokens 超出模型单次输出上限 {model.MaxOutputTokens}。");
        }

        return null;
    }

    private void AddBindings(long agentId, IEnumerable<long> toolIds, IEnumerable<long> knowledgeBaseIds)
    {
        foreach (var toolId in toolIds.Distinct())
        {
            _db.AgentTools.Add(new AgentTool { AgentId = agentId, ToolId = toolId });
        }

        foreach (var knowledgeBaseId in knowledgeBaseIds.Distinct())
        {
            _db.AgentKnowledges.Add(new AgentKnowledge { AgentId = agentId, KnowledgeBaseId = knowledgeBaseId });
        }
    }

    // 全量替换：与现存绑定差量比对，多的软删、少的新增。重复 ID 在请求校验层已排除。
    private async Task ReplaceBindingsAsync(long agentId, IReadOnlyList<long> toolIds, IReadOnlyList<long> knowledgeBaseIds, CancellationToken cancellationToken)
    {
        var desiredTools = toolIds.ToHashSet();
        var existingTools = await _db.AgentTools.Where(binding => binding.AgentId == agentId).ToListAsync(cancellationToken);
        _db.AgentTools.RemoveRange(existingTools.Where(binding => !desiredTools.Contains(binding.ToolId)));
        var existingToolIds = existingTools.Select(binding => binding.ToolId).ToHashSet();
        foreach (var toolId in desiredTools.Where(toolId => !existingToolIds.Contains(toolId)))
        {
            _db.AgentTools.Add(new AgentTool { AgentId = agentId, ToolId = toolId });
        }

        var desiredKnowledge = knowledgeBaseIds.ToHashSet();
        var existingKnowledge = await _db.AgentKnowledges.Where(binding => binding.AgentId == agentId).ToListAsync(cancellationToken);
        _db.AgentKnowledges.RemoveRange(existingKnowledge.Where(binding => !desiredKnowledge.Contains(binding.KnowledgeBaseId)));
        var existingKnowledgeIds = existingKnowledge.Select(binding => binding.KnowledgeBaseId).ToHashSet();
        foreach (var knowledgeBaseId in desiredKnowledge.Where(knowledgeBaseId => !existingKnowledgeIds.Contains(knowledgeBaseId)))
        {
            _db.AgentKnowledges.Add(new AgentKnowledge { AgentId = agentId, KnowledgeBaseId = knowledgeBaseId });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(IReadOnlyList<long> ToolIds, IReadOnlyList<long> KnowledgeBaseIds)> LoadBindingsAsync(long agentId, CancellationToken cancellationToken)
    {
        var toolIds = await _db.AgentTools.AsNoTracking()
            .Where(binding => binding.AgentId == agentId)
            .Select(binding => binding.ToolId)
            .ToListAsync(cancellationToken);
        var knowledgeBaseIds = await _db.AgentKnowledges.AsNoTracking()
            .Where(binding => binding.AgentId == agentId)
            .Select(binding => binding.KnowledgeBaseId)
            .ToListAsync(cancellationToken);
        return (toolIds, knowledgeBaseIds);
    }
}
