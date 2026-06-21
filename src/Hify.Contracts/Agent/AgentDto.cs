namespace Hify.Contracts.Agent;

/// <summary>
/// Agent 配置视图。Agent 为纯配置存储，仅持有引用 ID（模型、工具、知识库），运行时由对话引擎装配。
/// 供模块间引用与管理 API 返回共用。
/// </summary>
public record AgentDto
{
    /// <summary>主键。</summary>
    public long Id { get; init; }

    /// <summary>名称（同一未删集合内唯一）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>引用的 chat 模型 Id（-&gt; model_provider.model）。</summary>
    public long ModelId { get; init; }

    /// <summary>系统提示词。</summary>
    public string SystemPrompt { get; init; } = string.Empty;

    /// <summary>模型生成参数。</summary>
    public ModelParams ModelParams { get; init; } = new();

    /// <summary>RAG 检索参数。</summary>
    public RetrievalParams RetrievalParams { get; init; } = new();

    /// <summary>工具调用循环上限，防止无限循环耗尽 token。</summary>
    public int MaxIterations { get; init; }

    /// <summary>绑定的 MCP 工具 Id 列表（-&gt; mcp.mcp_tool）。无绑定为空列表。</summary>
    public IReadOnlyList<long> ToolIds { get; init; } = [];

    /// <summary>绑定的知识库 Id 列表（-&gt; knowledge.knowledge_base）。无绑定为空列表。</summary>
    public IReadOnlyList<long> KnowledgeBaseIds { get; init; } = [];

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; }

    /// <summary>创建时刻（epoch ms）。</summary>
    public long CreatedAt { get; init; }

    /// <summary>最后更新时刻（epoch ms）。</summary>
    public long UpdatedAt { get; init; }
}
