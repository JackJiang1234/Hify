using Hify.Shared.Caching;

namespace Hify.Modules.Conversation.Features.Context;

/// <summary>会话历史缓存项（裁剪前的近期消息，仅取装配上下文所需字段）。</summary>
internal sealed record CachedMessage(string Role, string Content);

/// <summary>
/// 会话近期历史的 Cache-Aside 缓存（key <c>hify:conversation:ctx:{id}</c>，滑动 TTL）。
/// 读优先缓存、未命中回源并回填；落新消息后失效。Redis 故障由底层 <see cref="ICacheService"/> 静默降级，
/// 退化为每次回源、不丢历史（事实来源始终是 PostgreSQL）。
/// </summary>
internal sealed class ConversationContextCache
{
    // 会话有活跃期，用 TTL 让冷会话自动过期、释放内存。
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private readonly ICacheService _cache;

    public ConversationContextCache(ICacheService cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    /// <summary>取近期历史：命中直接返回，未命中调用 <paramref name="loader"/> 回源并回填。</summary>
    public Task<IReadOnlyList<CachedMessage>> GetOrLoadAsync(
        long conversationId,
        Func<CancellationToken, Task<IReadOnlyList<CachedMessage>>> loader,
        CancellationToken cancellationToken) =>
        _cache.GetOrSetAsync(Key(conversationId), Ttl, loader, cancellationToken);

    /// <summary>失效某会话的历史缓存（落新消息后调用）。</summary>
    public Task InvalidateAsync(long conversationId, CancellationToken cancellationToken) =>
        _cache.RemoveAsync(Key(conversationId), cancellationToken);

    private static string Key(long conversationId) => CacheKey.For("conversation", "ctx", conversationId);
}
