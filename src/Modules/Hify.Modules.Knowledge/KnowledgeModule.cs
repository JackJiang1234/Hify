using Hify.Shared.Modularity;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hify.Modules.Knowledge;

/// <summary>
/// Knowledge 模块注册入口（L1 领域能力，依赖 ModelProvider 计算 embedding，仅经 Contracts）。
/// 负责知识库与 RAG（一期 TXT 文档、固定长度分块、pgvector 检索）。
/// </summary>
public sealed class KnowledgeModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: 注册文档分块/嵌入/检索服务、DbContext（关系数据 + pgvector 向量表）。
    }
}
