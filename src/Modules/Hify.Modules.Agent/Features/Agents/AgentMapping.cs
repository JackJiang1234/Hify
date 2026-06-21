using Hify.Contracts.Agent;

namespace Hify.Modules.Agent.Features.Agents;

/// <summary>实体 + 绑定 → <see cref="AgentDto"/> 映射。jsonb 文本在此反序列化为强类型参数。</summary>
internal static class AgentMapping
{
    public static AgentDto ToDto(Domain.Agent agent, IReadOnlyList<long> toolIds, IReadOnlyList<long> knowledgeBaseIds) => new()
    {
        Id = agent.Id,
        Name = agent.Name,
        Description = agent.Description,
        ModelId = agent.ModelId,
        SystemPrompt = agent.SystemPrompt,
        ModelParams = AgentParamsJson.DeserializeModelParams(agent.ModelParams),
        RetrievalParams = AgentParamsJson.DeserializeRetrievalParams(agent.RetrievalParams),
        MaxIterations = agent.MaxIterations,
        ToolIds = toolIds,
        KnowledgeBaseIds = knowledgeBaseIds,
        Enabled = agent.Enabled,
        CreatedAt = agent.CreatedAt,
        UpdatedAt = agent.UpdatedAt,
    };
}
