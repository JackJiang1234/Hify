using Hify.Contracts.Agent;
using Hify.Contracts.Mcp;
using Hify.Contracts.ModelProvider;
using Hify.Modules.Conversation.Domain;
using Hify.Modules.Conversation.Features.Context;
using Hify.Modules.Conversation.Features.Retrieval;
using Hify.Modules.Conversation.Persistence;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Conversation.Features.Chat;

/// <summary>装配好的一次模型调用：解析出的模型 Id + 供应商无关的对话请求 + 工具名→工具 Id 映射。</summary>
/// <param name="ModelId">实际使用的模型 Id。</param>
/// <param name="Request">对话请求（含 system + 裁剪历史 + 本次输入；启用工具时含 Tools）。</param>
/// <param name="ToolIdsByName">工具名 → MCP 工具 Id 映射，供工具循环把模型的 tool_call 名解析回调用。无工具为空。</param>
/// <param name="MaxIterations">工具调用循环上限（来自 Agent 配置），防止无限循环耗 token。</param>
internal sealed record PreparedChat(
    long ModelId,
    ChatRequest Request,
    IReadOnlyDictionary<string, long> ToolIdsByName,
    int MaxIterations);

/// <summary>
/// 上下文装配：取 Agent 配置 + 模型元数据 + RAG（seam）+ 裁剪后的历史，组装供应商无关的 <see cref="ChatRequest"/>。
/// 可预期失败以 <see cref="Result{T}"/>（4xxx）返回，不抛异常。不在此落库用户消息（由编排器负责）。
/// </summary>
internal sealed class ContextBuilder
{
    // 预算安全余量：估算偏差 + 供应商侧不可见开销的缓冲。
    private const int SafetyMarginTokens = 256;

    // 回源时只取近期 N 条历史：裁剪本就会丢更早的，限制此处可界定 DB 查询与缓存体积。
    private const int MaxHistoryMessages = 50;

    private static readonly IReadOnlyDictionary<string, long> EmptyToolMap = new Dictionary<string, long>();

    private readonly ConversationDbContext _db;
    private readonly IAgentQuery _agents;
    private readonly IModelProviderQuery _models;
    private readonly IRetriever _retriever;
    private readonly ITokenEstimator _estimator;
    private readonly ConversationContextCache _cache;
    private readonly IMcpToolQuery _tools;

    public ContextBuilder(
        ConversationDbContext db,
        IAgentQuery agents,
        IModelProviderQuery models,
        IRetriever retriever,
        ITokenEstimator estimator,
        ConversationContextCache cache,
        IMcpToolQuery tools)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(retriever);
        ArgumentNullException.ThrowIfNull(estimator);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(tools);
        _db = db;
        _agents = agents;
        _models = models;
        _retriever = retriever;
        _estimator = estimator;
        _cache = cache;
        _tools = tools;
    }

    /// <summary>装配一次对话调用。</summary>
    /// <param name="conversationId">会话 Id（用于取历史）。</param>
    /// <param name="agentId">会话绑定的 Agent Id。</param>
    /// <param name="userInput">本次用户输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<Result<PreparedChat>> BuildAsync(
        long conversationId,
        long agentId,
        string userInput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userInput);

        var agentResult = await _agents.GetAgentAsync(agentId, cancellationToken);
        if (agentResult.Code != 200 || agentResult.Data is null)
        {
            return Result<PreparedChat>.Fail((int)ChatErrorCode.AgentUnavailable, "Agent 不存在或已停用。");
        }

        var agent = agentResult.Data;
        if (!agent.Enabled)
        {
            return Result<PreparedChat>.Fail((int)ChatErrorCode.AgentUnavailable, "Agent 已停用。");
        }

        var modelResult = await _models.GetModelAsync(agent.ModelId, cancellationToken);
        if (modelResult.Code != 200 || modelResult.Data is null)
        {
            return Result<PreparedChat>.Fail((int)ChatErrorCode.ModelUnavailable, "Agent 绑定的模型不存在。");
        }

        var model = modelResult.Data;
        if (!model.Enabled || model.ModelType != ModelTypes.Chat)
        {
            return Result<PreparedChat>.Fail((int)ChatErrorCode.ModelUnavailable, "Agent 绑定的模型不可用。");
        }

        // 系统内容 = 系统提示词 + RAG 片段（一期 seam 恒空）。
        var systemContent = await ComposeSystemContentAsync(agent, userInput, cancellationToken);

        var maxOutput = ResolveMaxOutput(agent.ModelParams.MaxTokens, model.MaxOutputTokens);

        // 留给历史的 token 预算；ContextWindow<=0 视为未知，不裁剪。
        var historyBudget = int.MaxValue;
        if (model.ContextWindow > 0)
        {
            var available = model.ContextWindow
                - maxOutput
                - _estimator.Estimate(systemContent)
                - _estimator.Estimate(userInput)
                - SafetyMarginTokens;
            if (available < 0)
            {
                return Result<PreparedChat>.Fail((int)ChatErrorCode.ContextOverflow, "系统提示词与本次输入已超出模型上下文窗口。");
            }

            historyBudget = (int)Math.Min(available, int.MaxValue);
        }

        var history = await LoadHistoryAsync(conversationId, cancellationToken);
        var trimmed = ContextWindowTrimmer.Trim(history, historyBudget, _estimator);

        var messages = new List<ChatMessage>(trimmed.Count + 2);
        if (!string.IsNullOrEmpty(systemContent))
        {
            messages.Add(new ChatMessage { Role = MessageRoles.System, Content = systemContent });
        }

        messages.AddRange(trimmed);
        messages.Add(new ChatMessage { Role = MessageRoles.User, Content = userInput });

        var (toolDefinitions, toolIdsByName) = await ResolveToolsAsync(agent, model, cancellationToken);

        var request = new ChatRequest
        {
            Messages = messages,
            MaxTokens = maxOutput,
            Temperature = agent.ModelParams.Temperature,
            TopP = agent.ModelParams.TopP,
            Tools = toolDefinitions,
        };

        return Result<PreparedChat>.Ok(new PreparedChat(model.Id, request, toolIdsByName, agent.MaxIterations));
    }

    /// <summary>
    /// 解析 Agent 可调用的工具：仅当模型支持工具且绑定非空时启用，映射为供应商无关的 <see cref="ToolDefinition"/>，
    /// 并返回工具名→工具 Id 映射供循环回指。无工具/模型不支持/查询失败均降级为空（走纯文本路径）。
    /// </summary>
    private async Task<(IReadOnlyList<ToolDefinition> Definitions, IReadOnlyDictionary<string, long> IdsByName)> ResolveToolsAsync(
        AgentDto agent, ModelDto model, CancellationToken cancellationToken)
    {
        if (!model.SupportsTools || agent.ToolIds.Count == 0)
        {
            return ([], EmptyToolMap);
        }

        var result = await _tools.GetInvocableToolsAsync(agent.ToolIds, cancellationToken);
        if (result.Code != 200 || result.Data is null || result.Data.Count == 0)
        {
            return ([], EmptyToolMap);
        }

        var definitions = new List<ToolDefinition>(result.Data.Count);
        var idsByName = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var tool in result.Data)
        {
            // 工具名在 Agent 工具集内应唯一；重名以首个为准（模型只见名字，无法区分同名）。
            if (idsByName.TryAdd(tool.Name, tool.Id))
            {
                definitions.Add(new ToolDefinition
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    ParametersJson = tool.InputSchema,
                });
            }
        }

        return (definitions, idsByName);
    }

    private async Task<string> ComposeSystemContentAsync(AgentDto agent, string userInput, CancellationToken cancellationToken)
    {
        if (agent.KnowledgeBaseIds.Count == 0)
        {
            return agent.SystemPrompt;
        }

        var chunks = await _retriever.RetrieveAsync(
            agent.KnowledgeBaseIds,
            userInput,
            agent.RetrievalParams.TopK,
            agent.RetrievalParams.ScoreThreshold,
            cancellationToken);
        if (chunks.Count == 0)
        {
            return agent.SystemPrompt;
        }

        var knowledge = string.Join("\n\n", chunks.Select(chunk => chunk.Content));
        return string.IsNullOrEmpty(agent.SystemPrompt)
            ? knowledge
            : $"{agent.SystemPrompt}\n\n参考资料：\n{knowledge}";
    }

    private async Task<IReadOnlyList<ChatMessage>> LoadHistoryAsync(long conversationId, CancellationToken cancellationToken)
    {
        var cached = await _cache.GetOrLoadAsync(conversationId, LoadRecentFromDbAsync, cancellationToken);
        return cached.Select(row => new ChatMessage { Role = row.Role, Content = row.Content }).ToList();

        async Task<IReadOnlyList<CachedMessage>> LoadRecentFromDbAsync(CancellationToken ct)
        {
            // 仅取已完成的 user/assistant 文本消息；按 id 倒序取近期 N 条后翻回旧→新。
            // 排除工具循环的中间消息：assistant 发起工具调用的轮（ToolCalls!="[]"）与 tool 结果消息不作历史，
            // 只保留用户输入与最终文本回复（ToolCalls=="[]"）。
            var recent = await _db.Messages.AsNoTracking()
                .Where(m => m.ConversationId == conversationId
                    && m.Status == MessageStatus.Completed
                    && (m.Role == MessageRoles.User || m.Role == MessageRoles.Assistant)
                    && m.ToolCalls == "[]")
                .OrderByDescending(m => m.Id)
                .Take(MaxHistoryMessages)
                .Select(m => new CachedMessage(m.Role, m.Content))
                .ToListAsync(ct);

            recent.Reverse();
            return recent;
        }
    }

    private static int ResolveMaxOutput(int? agentMaxTokens, long modelMaxOutput)
    {
        if (agentMaxTokens is int requested && requested > 0)
        {
            return requested;
        }

        // 模型未声明上限时给一个稳妥默认，避免请求缺省 MaxTokens=0。
        return modelMaxOutput > 0 ? (int)Math.Min(modelMaxOutput, int.MaxValue) : 1024;
    }
}
