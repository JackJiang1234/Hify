using Hify.Shared.Persistence;

namespace Hify.Modules.ModelProvider.Domain;

/// <summary>
/// 供应商下的具体模型（chat/embedding）。一期仅手动录入，<see cref="Source"/> 恒为 <c>manual</c>。
/// 通过 <see cref="ProviderId"/> 关联 <see cref="Provider"/>（应用层维护引用，不建库级外键）。
/// </summary>
internal sealed class Model : EntityBase
{
    /// <summary>所属供应商 Id。</summary>
    public long ProviderId { get; set; }

    /// <summary>模型标识（API 侧名称），如 <c>gpt-4o</c> / <c>claude-opus-4-8</c>。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>展示名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>模型类型：<c>chat</c> | <c>embedding</c>。</summary>
    public string ModelType { get; set; } = string.Empty;

    /// <summary>上下文窗口 token 数。</summary>
    public long ContextWindow { get; set; }

    /// <summary>单次最大输出 token 数。</summary>
    public long MaxOutputTokens { get; set; }

    /// <summary>嵌入维度（仅 embedding 模型有意义，启用前对照固定 1536 校验）。</summary>
    public int EmbeddingDimensions { get; set; }

    /// <summary>是否支持流式响应。</summary>
    public bool SupportsStreaming { get; set; }

    /// <summary>是否支持工具调用。</summary>
    public bool SupportsTools { get; set; }

    /// <summary>是否支持视觉输入。</summary>
    public bool SupportsVision { get; set; }

    /// <summary>来源：一期仅 <c>manual</c>（手动录入）。</summary>
    public string Source { get; set; } = "manual";

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>是否为该供应商该类型的默认模型（每供应商每类型至多一个）。</summary>
    public bool IsDefault { get; set; }

    /// <summary>展示排序。</summary>
    public int SortOrder { get; set; }
}
