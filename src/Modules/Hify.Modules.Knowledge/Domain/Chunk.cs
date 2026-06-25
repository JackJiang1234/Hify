using Hify.Shared.Persistence;

using Pgvector;

namespace Hify.Modules.Knowledge.Domain;

/// <summary>
/// 文档分块 + 向量。关系数据存 PostgreSQL，向量存 pgvector（维度固定 1536）。
/// <see cref="KnowledgeBaseId"/> 为冗余列，便于按库直接检索（空间换时间）。
/// </summary>
internal sealed class Chunk : EntityBase
{
    /// <summary>来源文档 Id（-&gt; knowledge.document）。</summary>
    public long DocumentId { get; set; }

    /// <summary>所属知识库 Id（冗余 -&gt; knowledge.knowledge_base，便于按库检索）。</summary>
    public long KnowledgeBaseId { get; set; }

    /// <summary>文档内分块序号（0 起）。</summary>
    public int ChunkIndex { get; set; }

    /// <summary>分块文本。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 1536 维向量。数据库列 <c>vector(1536) NOT NULL</c> 无有意义默认值，插入时必给；
    /// 实体侧以 <c>null!</c> 占位（构造时总会赋值），故就地抑制可空告警。
    /// </summary>
    public Vector Embedding { get; set; } = null!;
}
