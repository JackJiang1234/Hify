using Hify.Modules.Agent.Persistence;
using Hify.Shared.Configuration;
using Hify.Shared.Modularity;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hify.Modules.Agent;

/// <summary>
/// Agent 模块注册入口（L1 领域能力，纯配置存储，仅存引用 ID）。
/// 负责 Agent 的创建与配置（选模型、绑工具、设系统提示词）；模型引用经 IModelProviderQuery（L0）校验。
/// 控制器与 FluentValidation 校验器由 Host 自动发现，无需在此注册。
/// </summary>
public sealed class AgentModule : IModule
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 独立 DbContext / 独立 schema；连接串由全局 DatabaseOptions 构建。不启用 Migrations（DDL 手写）。
        services.AddDbContext<AgentDbContext>((provider, options) =>
        {
            var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(BuildConnectionString(database));
        });

        // Agent 配置 CRUD 服务（依赖 DbContext + 跨模块 IModelProviderQuery，注册为 Scoped）。
        services.AddScoped<Features.Agents.AgentService>();
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
