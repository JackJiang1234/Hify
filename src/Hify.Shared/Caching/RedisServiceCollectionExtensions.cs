using Hify.Shared.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Hify.Shared.Caching;

/// <summary>
/// Redis 缓存装配。注册共享的 <see cref="IConnectionMultiplexer"/>（单例、懒连接）与 <see cref="ICacheService"/>。
/// 需先绑定 <see cref="RedisOptions"/>。
/// </summary>
public static class RedisServiceCollectionExtensions
{
    /// <summary>注册 Redis 连接与缓存服务。</summary>
    /// <param name="services">DI 服务集合。</param>
    public static IServiceCollection AddHifyRedis(this IServiceCollection services)
    {
        services.TryAddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(RedisConnectionFactory.BuildConfiguration(options));
        });

        services.TryAddSingleton<ICacheService, RedisCacheService>();

        return services;
    }
}
