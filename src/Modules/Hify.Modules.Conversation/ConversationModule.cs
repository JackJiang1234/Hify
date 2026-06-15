using Hify.Shared.Modularity;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hify.Modules.Conversation;

/// <summary>
/// Conversation 模块注册入口（L2 编排层，依赖 Agent/ModelProvider/Knowledge/Mcp，仅经 Contracts）。
/// 负责对话引擎：流式响应、多轮对话、上下文管理。
/// </summary>
public sealed class ConversationModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: 注册对话编排、流式（SSE）、上下文管理服务、DbContext（message 大表索引）。
    }
}
