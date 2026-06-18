namespace Hify.Contracts.ModelProvider;

/// <summary>
/// 模型元数据（含能力位）。供 Agent/Conversation/Knowledge 解析引用，与管理 API 返回共用。
/// </summary>
public record ModelDto
{
    /// <summary>主键。</summary>
    public long Id { get; init; }

    /// <summary>所属供应商 Id。</summary>
    public long ProviderId { get; init; }

    /// <summary>模型标识（API 侧名称，如 <c>gpt-4o</c> / <c>claude-opus-4-8</c>）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>展示名称。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>模型类型，见 <see cref="ModelTypes"/>。</summary>
    public string ModelType { get; init; } = string.Empty;

    /// <summary>上下文窗口 token 数。</summary>
    public long ContextWindow { get; init; }

    /// <summary>单次最大输出 token 数。</summary>
    public long MaxOutputTokens { get; init; }

    /// <summary>嵌入维度（仅嵌入模型有意义）。</summary>
    public int EmbeddingDimensions { get; init; }

    /// <summary>是否支持流式响应。</summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>是否支持工具调用。</summary>
    public bool SupportsTools { get; init; }

    /// <summary>是否支持视觉输入。</summary>
    public bool SupportsVision { get; init; }

    /// <summary>来源，见 <see cref="ModelSources"/>。</summary>
    public string Source { get; init; } = ModelSources.Manual;

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; }

    /// <summary>是否为该供应商该类型的默认模型。</summary>
    public bool IsDefault { get; init; }

    /// <summary>展示排序。</summary>
    public int SortOrder { get; init; }

    /// <summary>创建时刻（epoch ms）。</summary>
    public long CreatedAt { get; init; }

    /// <summary>最后更新时刻（epoch ms）。</summary>
    public long UpdatedAt { get; init; }
}
