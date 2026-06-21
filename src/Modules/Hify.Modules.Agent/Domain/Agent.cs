using Hify.Shared.Persistence;

namespace Hify.Modules.Agent.Domain;

/// <summary>
/// Agent 配置实体（纯配置存储，仅持有引用 ID）。引用完整性由应用层维护，不建库级外键。
/// 生成参数 <see cref="ModelParams"/> 与检索参数 <see cref="RetrievalParams"/> 以 jsonb 文本存储，
/// 收发口由应用服务做强类型序列化/反序列化（落库前已校验，故存的是可信文本）。
/// 工具与知识库绑定分别存于 <see cref="AgentTool"/> / <see cref="AgentKnowledge"/> 关联表。
/// </summary>
internal sealed class Agent : EntityBase
{
    /// <summary>名称（同一未删集合内唯一）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>引用的 chat 模型 Id（-&gt; model_provider.model）。</summary>
    public long ModelId { get; set; }

    /// <summary>系统提示词。</summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>模型生成参数（jsonb 文本，如 <c>{"temperature":0.7}</c>）。</summary>
    public string ModelParams { get; set; } = "{}";

    /// <summary>RAG 检索参数（jsonb 文本，如 <c>{"topK":3,"scoreThreshold":0.5}</c>）。</summary>
    public string RetrievalParams { get; set; } = "{}";

    /// <summary>工具调用循环上限。</summary>
    public int MaxIterations { get; set; } = 5;

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; } = true;
}
