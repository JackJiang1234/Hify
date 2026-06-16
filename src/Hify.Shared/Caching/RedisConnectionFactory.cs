using Hify.Shared.Configuration;

using StackExchange.Redis;

namespace Hify.Shared.Caching;

/// <summary>
/// 由 <see cref="RedisOptions"/> 构建 StackExchange.Redis 连接配置。
/// <c>AbortOnConnectFail = false</c>：Redis 暂时不可用时不阻断启动，连接恢复后自动重连，
/// 配合 <see cref="RedisCacheService"/> 的降级逻辑保证缓存故障不拖垮业务。
/// </summary>
public static class RedisConnectionFactory
{
    /// <summary>构建连接配置。</summary>
    /// <param name="options">Redis 配置。</param>
    public static ConfigurationOptions BuildConfiguration(RedisOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configuration = new ConfigurationOptions
        {
            DefaultDatabase = options.Database,
            ConnectTimeout = options.ConnectTimeoutMs,
            AbortOnConnectFail = false,
            ClientName = CacheKey.Prefix,
        };

        configuration.EndPoints.Add(options.Host, options.Port);

        if (!string.IsNullOrEmpty(options.Password))
        {
            configuration.Password = options.Password;
        }

        return configuration;
    }
}
