namespace Hify.Modules.Knowledge.Features.Documents;

/// <summary>文档视图（模块内管理 API 返回用）。非跨模块契约，故不置于 Hify.Contracts。</summary>
internal sealed record DocumentDto
{
    /// <summary>主键。</summary>
    public long Id { get; init; }

    /// <summary>所属知识库 Id。</summary>
    public long KnowledgeBaseId { get; init; }

    /// <summary>文件名。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>文件类型（一期 <c>txt</c>）。</summary>
    public string FileType { get; init; } = string.Empty;

    /// <summary>内容哈希（SHA256 十六进制）。</summary>
    public string ContentHash { get; init; } = string.Empty;

    /// <summary>处理状态。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>原文字符数。</summary>
    public long CharCount { get; init; }

    /// <summary>已生成分块数。</summary>
    public int ChunkCount { get; init; }

    /// <summary>处理失败原因。</summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>创建时刻（epoch ms）。</summary>
    public long CreatedAt { get; init; }

    /// <summary>最后更新时刻（epoch ms）。</summary>
    public long UpdatedAt { get; init; }
}
