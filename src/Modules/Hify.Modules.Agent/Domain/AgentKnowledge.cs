using Hify.Shared.Persistence;

namespace Hify.Modules.Agent.Domain;

/// <summary>
/// Agent 与知识库的绑定（多对多关联行，用于 RAG）。两侧 Id 均为应用层维护的引用，不建库级外键。
/// </summary>
internal sealed class AgentKnowledge : EntityBase
{
    /// <summary>所属 Agent Id（-&gt; agent.agent）。</summary>
    public long AgentId { get; set; }

    /// <summary>绑定的知识库 Id（-&gt; knowledge.knowledge_base）。</summary>
    public long KnowledgeBaseId { get; set; }
}
