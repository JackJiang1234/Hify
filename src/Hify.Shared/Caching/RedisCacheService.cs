using System.Text.Json;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace Hify.Shared.Caching;

/// <summary>
/// 基于 StackExchange.Redis 的 <see cref="ICacheService"/> 实现。值以 JSON 序列化存储；
/// 所有 Redis 操作包裹故障降级：连接/超时异常仅告警并退化（读取当未命中、回源直返、写入忽略），
/// 不向上抛出，避免缓存故障阻断业务。
/// </summary>
internal sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connection;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer connection, ILogger<RedisCacheService> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(logger);
        _connection = connection;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var raw = await _connection.GetDatabase().StringGetAsync(key);
            return raw.IsNull ? default : Deserialize<T>(raw!);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "缓存读取失败，降级为未命中 {CacheKey}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        cancellationToken.ThrowIfCancellationRequested();

        await TrySetAsync(key, value, ttl);
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await _connection.GetDatabase().KeyDeleteAsync(key);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "缓存删除失败，忽略 {CacheKey}", key);
            return false;
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(factory);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var raw = await _connection.GetDatabase().StringGetAsync(key);
            if (!raw.IsNull)
            {
                // 命中（含已缓存的空值字面量 "null"，防穿透）。
                return Deserialize<T>(raw!)!;
            }
        }
        catch (RedisException ex)
        {
            // 缓存故障：直接回源，不回填，保证业务可用。
            _logger.LogWarning(ex, "缓存读取失败，回源 {CacheKey}", key);
            return await factory(cancellationToken);
        }

        var value = await factory(cancellationToken);
        await TrySetAsync(key, value, ttl);
        return value;
    }

    private async Task TrySetAsync<T>(string key, T value, TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "TTL 必须为正。");
        }

        try
        {
            var payload = JsonSerializer.Serialize(value, JsonOptions);
            await _connection.GetDatabase().StringSetAsync(key, payload, ttl);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "缓存写入失败，忽略 {CacheKey}", key);
        }
    }

    private static T? Deserialize<T>(string raw) => JsonSerializer.Deserialize<T>(raw, JsonOptions);
}
