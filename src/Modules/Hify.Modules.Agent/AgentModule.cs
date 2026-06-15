using Hify.Shared.Modularity;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hify.Modules.Agent;

/// <summary>
/// Agent 模块注册入口（L1 领域能力，纯配置存储，仅存引用 ID）。
/// 负责 Agent 的创建与配置（选模型、绑工具、设系统提示词）。
/// </summary>
public sealed class AgentModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: 注册 Agent 配置的 Feature 处理器、DbContext（独立 schema）。
    }
}
