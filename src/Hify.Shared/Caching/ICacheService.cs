namespace Hify.Shared.Caching;

/// <summary>
/// 缓存服务（Cache-Aside）。统一序列化与键约定；Redis 故障时降级而非抛异常，
/// 不阻断业务流（读取退化为未命中、<see cref="GetOrSetAsync{T}"/> 回源、写入失败忽略）。
/// 键请用 <see cref="CacheKey"/> 生成；TTL 必须为正。
/// </summary>
public interface ICacheService
{
    /// <summary>读取缓存值。未命中或 Redis 故障返回 <c>default</c>。</summary>
    /// <typeparam name="T">值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>写入缓存值并设置过期时间。Redis 故障时静默忽略。</summary>
    /// <typeparam name="T">值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="value">值（可为 <c>null</c>，用于显式缓存空值防穿透）。</param>
    /// <param name="ttl">过期时间，必须为正。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>删除缓存键。返回是否删除成功；Redis 故障返回 <c>false</c>。</summary>
    /// <param name="key">缓存键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache-Aside 读取：命中直接返回（含已缓存的空值，防穿透）；未命中调用 <paramref name="factory"/>
    /// 回源、回填后返回。Redis 故障时回源直返、不回填。
    /// </summary>
    /// <typeparam name="T">值类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="ttl">回填过期时间，必须为正。</param>
    /// <param name="factory">回源委托（缓存未命中时调用）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<T> GetOrSetAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default);
}
