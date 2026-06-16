using Hify.Shared.Caching;
using Hify.Shared.Configuration;
using Hify.Shared.Time;

namespace Hify.Host.Configuration;

/// <summary>
/// 绑定并校验全局配置（数据库、Redis），并注册共享基础设施（时间源、Redis 缓存）。
/// 缺失或非法配置（如未注入密码）在启动时即失败。
/// </summary>
internal static class ConfigurationHostExtensions
{
    public static IServiceCollection AddHifyConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHifyRedis();

        return services;
    }
}
