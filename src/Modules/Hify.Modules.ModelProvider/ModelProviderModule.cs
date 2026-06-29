using Hify.Contracts.ModelProvider;

using Hify.Modules.ModelProvider.Adapters;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Shared.Configuration;
using Hify.Shared.Modularity;
using Hify.Shared.Resilience;
using Hify.Shared.Security;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hify.Modules.ModelProvider;

/// <summary>
/// ModelProvider 模块注册入口（L0 基础能力，不依赖任何业务模块）。
/// 负责多模型提供商（OpenAI/Claude/Ollama）适配与管理；适配走裸 HttpClient（方案 B，无 SDK）。
/// </summary>
public sealed class ModelProviderModule : IModule
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 独立 DbContext / 独立 schema；连接串由全局 DatabaseOptions 构建。不启用 Migrations（DDL 手写）。
        services.AddDbContext<ModelProviderDbContext>((provider, options) =>
        {
            var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(BuildConnectionString(database));
        });

        // 凭证加密（app 级共享）：密钥从配置注入。延迟校验（单例首次解析时），缺失密钥不阻断无关模块的 Host 启动。
        services.AddHifyCredentialProtection(configuration);

        // 适配器（裸 HttpClient + resilience）：每类型两组命名客户端——同步（60s、重试）与流式（120s、不重试）。
        // 超时/熔断/舱壁参数后续外化到配置（P8）；此处先用规范默认值。
        AddProviderHttpClients(services, OpenAiCompatibleAdapter.SyncClientName, OpenAiCompatibleAdapter.StreamClientName);
        AddProviderHttpClients(services, AnthropicAdapter.SyncClientName, AnthropicAdapter.StreamClientName);
        AddProviderHttpClients(services, OllamaAdapter.SyncClientName, OllamaAdapter.StreamClientName);

        services.AddSingleton<IModelProviderAdapter, OpenAiCompatibleAdapter>();
        services.AddSingleton<IModelProviderAdapter, AnthropicAdapter>();
        services.AddSingleton<IModelProviderAdapter, OllamaAdapter>();
        services.AddSingleton<IModelProviderAdapterFactory, ModelProviderAdapterFactory>();

        // 调用门面（跨模块）+ 供应商 CRUD 服务（依赖 DbContext，注册为 Scoped）。
        services.AddScoped<IModelInvoker, Invocation.ModelInvoker>();
        services.AddScoped<Features.Providers.ProviderService>();
        services.AddScoped<Features.Providers.ProviderConnectivityService>();

        // 周期健康探活（可配置、可关）：后台服务每轮新建 scope 复用连通性服务。
        services.AddOptions<Features.Providers.HealthProbeOptions>()
            .Bind(configuration.GetSection(Features.Providers.HealthProbeOptions.SectionName))
            .ValidateDataAnnotations();
        services.AddScoped<Features.Providers.ProviderHealthProbe>();
        services.AddHostedService<Features.Providers.ProviderHealthProbeService>();

        // 模型管理 + 跨模块只读查询。
        services.AddScoped<Features.Models.ModelService>();
        services.AddScoped<IModelProviderQuery, Features.Models.ModelProviderQuery>();
    }

    // 每个供应商类型两组命名客户端：同步（60s、重试）与流式（120s、不重试），各挂标准弹性管道。
    private static void AddProviderHttpClients(IServiceCollection services, string syncClientName, string streamClientName)
    {
        services.AddHttpClient(syncClientName)
            .AddHifyResilience(new ResilienceOptions());
        services.AddHttpClient(streamClientName)
            .AddHifyResilience(new ResilienceOptions { AttemptTimeoutSeconds = 120, RetryCount = 0 });
    }

    private static string BuildConnectionString(DatabaseOptions options) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Database,
            Username = options.Username,
            Password = options.Password,
            MaxPoolSize = options.MaxPoolSize,
            CommandTimeout = options.CommandTimeoutSeconds,
        }.ConnectionString;
}
