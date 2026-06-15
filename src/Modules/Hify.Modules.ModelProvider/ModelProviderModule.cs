using Hify.Shared.Modularity;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hify.Modules.ModelProvider;

/// <summary>
/// ModelProvider 模块注册入口（L0 基础能力，不依赖任何业务模块）。
/// 负责多模型提供商（OpenAI/Claude/Gemini/Ollama）适配与管理。
/// </summary>
public sealed class ModelProviderModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: 注册提供商适配器、DbContext（独立 schema）、Feature 处理器、熔断/舱壁策略。
    }
}
