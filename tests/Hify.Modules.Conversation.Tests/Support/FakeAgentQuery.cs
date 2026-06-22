using Hify.Contracts.Agent;
using Hify.Shared.Results;

namespace Hify.Modules.Conversation.Tests.Support;

/// <summary>
/// <see cref="IAgentQuery"/> 的内存替身：按 Id 返回预置 Agent 配置，未预置返回 NotFound（3001）。
/// 用于隔离对话引擎，无需启动 Agent 模块。
/// </summary>
internal sealed class FakeAgentQuery : IAgentQuery
{
    private readonly Dictionary<long, AgentDto> _agents = [];

    public FakeAgentQuery Add(AgentDto agent)
    {
        _agents[agent.Id] = agent;
        return this;
    }

    public static AgentDto ChatAgent(
        long id,
        long modelId,
        string systemPrompt = "you are helpful",
        bool enabled = true,
        IReadOnlyList<long>? knowledgeBaseIds = null) => new()
    {
        Id = id,
        Name = $"agent-{id}",
        ModelId = modelId,
        SystemPrompt = systemPrompt,
        ModelParams = new ModelParams(),
        RetrievalParams = new RetrievalParams { TopK = 3 },
        MaxIterations = 5,
        ToolIds = [],
        KnowledgeBaseIds = knowledgeBaseIds ?? [],
        Enabled = enabled,
    };

    public Task<Result<AgentDto>> GetAgentAsync(long agentId, CancellationToken cancellationToken) =>
        Task.FromResult(_agents.TryGetValue(agentId, out var agent)
            ? Result<AgentDto>.Ok(agent)
            : Result<AgentDto>.Fail(3001, "Agent 不存在。"));
}
