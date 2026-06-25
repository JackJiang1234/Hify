using Hify.Shared.Persistence;

namespace Hify.Modules.Knowledge.Domain;

/// <summary>
/// 文档实体。一期仅 TXT。上传只落元数据为 <see cref="DocumentStatuses.Pending"/>，
/// 分块/嵌入由处理流水线推进状态。不存原文（决策 3）：重切分靠重新上传。
/// <see cref="ContentHash"/> 用于同库内容去重与变更检测。引用完整性由应用层维护，不建库级外键。
/// </summary>
internal sealed class Document : EntityBase
{
    /// <summary>所属知识库 Id（-&gt; knowledge.knowledge_base）。</summary>
    public long KnowledgeBaseId { get; set; }

    /// <summary>文件名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>文件类型（一期恒为 <c>txt</c>）。</summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>内容哈希（SHA256 十六进制），用于去重/变更检测。</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>处理状态，见 <see cref="DocumentStatuses"/>。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>原文字符数。</summary>
    public long CharCount { get; set; }

    /// <summary>已生成分块数。</summary>
    public int ChunkCount { get; set; }

    /// <summary>处理失败原因（截断、不含敏感数据）。</summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
