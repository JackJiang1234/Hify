using Hify.Shared.Caching;

namespace Hify.Modules.Conversation.Tests.Support;

/// <summary>
/// 字典支撑的 <see cref="ICacheService"/>：真实实现 Cache-Aside 语义（命中返回、未命中回源回填、删除生效），
/// 用于确定性验证缓存包装的命中/失效行为，无需 Redis。真实 Redis 往返由 IntegrationTests 覆盖。
/// </summary>
internal sealed class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, object?> _store = [];

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.TryGetValue(key, out var value) ? (T?)value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Remove(key));

    public async Task<T> GetOrSetAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var cached))
        {
            return (T)cached!;
        }

        var produced = await factory(cancellationToken);
        _store[key] = produced;
        return produced;
    }
}
