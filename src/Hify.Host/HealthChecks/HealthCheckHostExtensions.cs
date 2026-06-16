using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hify.Host.HealthChecks;

/// <summary>
/// 健康检查装配：注册检查项与映射 <c>/api/v1/health</c> 端点。
/// </summary>
internal static class HealthCheckHostExtensions
{
    public static IServiceCollection AddHifyHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Host is running."));

        // TODO: DB/Redis 连接落地后追加就绪检查（AddNpgSql / AddRedis）。
        return services;
    }

    public static IEndpointRouteBuilder MapHifyHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/api/v1/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteAsync,
        });

        return endpoints;
    }
}
