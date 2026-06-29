using Hify.Contracts.Mcp;
using Hify.Modules.Mcp.Features.Invocation;
using Hify.Modules.Mcp.Features.Servers;
using Hify.Modules.Mcp.Features.Tools;
using Hify.Modules.Mcp.Persistence;
using Hify.Modules.Mcp.Protocol;
using Hify.Shared.Configuration;
using Hify.Shared.Modularity;
using Hify.Shared.Security;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hify.Modules.Mcp;

/// <summary>
/// MCP 模块注册入口（L0 基础能力，不依赖任何业务模块）。
/// 通过官方 SDK 以 Streamable HTTP 接入外部 MCP Server，提供工具发现与（并发）调用。
/// </summary>
public sealed class McpModule : IModule
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 独立 DbContext / 独立 schema；连接串由全局 DatabaseOptions 构建。不启用 Migrations（DDL 手写）。
        services.AddDbContext<McpDbContext>((provider, options) =>
        {
            var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(BuildConnectionString(database));
        });

        // 凭证加密（app 级共享，幂等：与 ModelProvider 共用同一把密钥）。
        services.AddHifyCredentialProtection(configuration);

        // 运行期配置（超时、并发、每-Server 弹性参数）。
        services.AddOptions<McpOptions>()
            .Bind(configuration.GetSection(McpOptions.SectionName))
            .ValidateDataAnnotations();

        // 协议层：命名 HttpClient 不设全局重试/熔断——每-Server 弹性由 McpResiliencePipelineProvider 提供；
        // 超时由调用方按 CallTimeoutSeconds / 行级 timeout_ms 用 CancellationToken 控制，故 HttpClient 不设超时。
        services.AddHttpClient(StreamableHttpMcpClient.HttpClientName)
            .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton<IMcpProtocolClient, StreamableHttpMcpClient>();
        services.AddSingleton<McpResiliencePipelineProvider>();

        // 功能服务（依赖 DbContext，Scoped）。
        services.AddScoped<McpServerService>();
        services.AddScoped<McpConnectivityService>();
        services.AddScoped<McpToolSyncService>();
        services.AddScoped<McpToolService>();

        // 跨模块契约：工具只读查询 + （并发）调用门面。
        services.AddScoped<IMcpToolQuery, McpToolQuery>();
        services.AddScoped<IMcpToolInvoker, McpToolInvoker>();
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
