using Hify.Host.Infrastructure;
using Hify.Host.Json;

using Hify.Modules.Agent;
using Hify.Modules.Conversation;
using Hify.Modules.Knowledge;
using Hify.Modules.Mcp;
using Hify.Modules.ModelProvider;
using Hify.Modules.Workflow;

using Hify.Shared.Modularity;

namespace Hify.Host.Modularity;

/// <summary>
/// 模块装配（组合根）：配置全局 MVC（Newtonsoft），注册各模块的控制器（ApplicationPart）与服务。
/// 模块按 L0→L1→L2 顺序列出，仅为可读性；DI 解析与顺序无关。
/// </summary>
internal static class ModuleHostExtensions
{
    public static IServiceCollection AddHifyModules(this IServiceCollection services, IConfiguration configuration)
    {
        var mvc = services
            .AddControllers()
            .AddNewtonsoftJson(options => HifyJsonSettings.Apply(options.SerializerSettings));

        // 允许发现 internal 控制器。
        mvc.ConfigureApplicationPartManager(manager =>
            manager.FeatureProviders.Add(new InternalControllerFeatureProvider()));

        foreach (var module in CreateModules())
        {
            // 将模块程序集纳入控制器发现范围（模块化单体：控制器分散在各模块）。
            mvc.AddApplicationPart(module.GetType().Assembly);
            module.RegisterServices(services, configuration);
        }

        return services;
    }

    private static IReadOnlyList<IModule> CreateModules() =>
    [
        // L0 基础能力
        new ModelProviderModule(),
        new McpModule(),
        // L1 领域能力
        new KnowledgeModule(),
        new AgentModule(),
        // L2 编排层
        new ConversationModule(),
        new WorkflowModule(),
    ];
}
