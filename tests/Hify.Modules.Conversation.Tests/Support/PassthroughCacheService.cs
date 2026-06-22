using Hify.Shared.Caching;

namespace Hify.Modules.Conversation.Tests.Support;

/// <summary>
/// 不缓存的 <see cref="ICacheService"/> 替身：GetOrSet 恒回源、Get 恒未命中、Set/Remove 空操作。
/// 用于隔离对话引擎逻辑（缓存命中/失效的真实行为另由 Redis 集成测试覆盖）。
/// </summary>
internal sealed class PassthroughCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<T?>(default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<T> GetOrSetAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default) =>
        factory(cancellationToken);
}
