using Hify.Modules.Conversation.Features.Context;
using Hify.Modules.Conversation.Tests.Support;

namespace Hify.Modules.Conversation.Tests.Features.Context;

/// <summary>
/// 会话历史缓存包装的行为测试（用内存 ICacheService，确定性、无需 Redis）：
/// 命中不回源、未命中回源回填、失效后重新回源。
/// </summary>
public sealed class ConversationContextCacheTests
{
    private const long ConversationId = 42;

    private static (ConversationContextCache Cache, Func<int> Calls, Func<CancellationToken, Task<IReadOnlyList<CachedMessage>>> Loader) Build()
    {
        var calls = 0;
        var cache = new ConversationContextCache(new InMemoryCacheService());
        Task<IReadOnlyList<CachedMessage>> Loader(CancellationToken ct)
        {
            calls++;
            return Task.FromResult<IReadOnlyList<CachedMessage>>([new CachedMessage("user", "hi")]);
        }

        return (cache, () => calls, Loader);
    }

    [Fact]
    public async Task GetOrLoad_Miss_CallsLoaderOnce_ThenServesFromCache()
    {
        var (cache, calls, loader) = Build();

        var first = await cache.GetOrLoadAsync(ConversationId, loader, CancellationToken.None);
        var second = await cache.GetOrLoadAsync(ConversationId, loader, CancellationToken.None);

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(1, calls()); // 第二次命中缓存，不再回源
    }

    [Fact]
    public async Task Invalidate_ForcesReloadOnNextGet()
    {
        var (cache, calls, loader) = Build();

        await cache.GetOrLoadAsync(ConversationId, loader, CancellationToken.None);
        await cache.InvalidateAsync(ConversationId, CancellationToken.None);
        await cache.GetOrLoadAsync(ConversationId, loader, CancellationToken.None);

        Assert.Equal(2, calls()); // 失效后再次回源
    }

    [Fact]
    public async Task DifferentConversations_AreIsolated()
    {
        var (cache, calls, loader) = Build();

        await cache.GetOrLoadAsync(1, loader, CancellationToken.None);
        await cache.GetOrLoadAsync(2, loader, CancellationToken.None);

        Assert.Equal(2, calls()); // 不同会话不共享缓存键
    }
}
