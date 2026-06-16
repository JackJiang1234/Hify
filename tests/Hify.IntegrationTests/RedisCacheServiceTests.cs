using Hify.Shared.Caching;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

namespace Hify.IntegrationTests;

/// <summary>
/// 缓存服务集成测试：默认对接配置中的真实 Redis（localhost:6379）；
/// Redis 不可达时静默跳过（早返回），避免在无 Redis 的环境中误报失败。
/// 降级用例使用一个指向无效端点的真实连接，验证故障不抛断业务（非 Mock）。
/// </summary>
public class RedisCacheServiceTests : IClassFixture<HifyTestFactory>
{
    private readonly HifyTestFactory _factory;

    public RedisCacheServiceTests(HifyTestFactory factory) => _factory = factory;

    private sealed record Sample(long Id, string Name);

    private bool TryGetLiveCache(out ICacheService cache)
    {
        var connection = _factory.Services.GetRequiredService<IConnectionMultiplexer>();
        cache = _factory.Services.GetRequiredService<ICacheService>();
        return connection.IsConnected;
    }

    [Fact]
    public async Task SetThenGet_RoundTripsValue()
    {
        if (!TryGetLiveCache(out var cache))
        {
            return;
        }

        var key = CacheKey.For("test", "sample", Guid.NewGuid());
        try
        {
            await cache.SetAsync(key, new Sample(7, "hify"), TimeSpan.FromMinutes(1));

            var got = await cache.GetAsync<Sample>(key);

            Assert.NotNull(got);
            Assert.Equal(7, got!.Id);
            Assert.Equal("hify", got.Name);
        }
        finally
        {
            await cache.RemoveAsync(key);
        }
    }

    [Fact]
    public async Task GetOrSet_Miss_CallsFactoryThenCachesForHit()
    {
        if (!TryGetLiveCache(out var cache))
        {
            return;
        }

        var key = CacheKey.For("test", "sample", Guid.NewGuid());
        var calls = 0;
        try
        {
            var first = await cache.GetOrSetAsync(
                key,
                TimeSpan.FromMinutes(1),
                _ =>
                {
                    calls++;
                    return Task.FromResult(new Sample(1, "a"));
                });

            var second = await cache.GetOrSetAsync(
                key,
                TimeSpan.FromMinutes(1),
                _ =>
                {
                    calls++;
                    return Task.FromResult(new Sample(1, "a"));
                });

            Assert.Equal(1, calls);
            Assert.Equal("a", first.Name);
            Assert.Equal("a", second.Name);
        }
        finally
        {
            await cache.RemoveAsync(key);
        }
    }

    [Fact]
    public async Task GetOrSet_CachesNull_PreventsPenetration()
    {
        if (!TryGetLiveCache(out var cache))
        {
            return;
        }

        var key = CacheKey.For("test", "nullable", Guid.NewGuid());
        var calls = 0;
        try
        {
            var first = await cache.GetOrSetAsync<Sample?>(
                key,
                TimeSpan.FromMinutes(1),
                _ =>
                {
                    calls++;
                    return Task.FromResult<Sample?>(null);
                });

            var second = await cache.GetOrSetAsync<Sample?>(
                key,
                TimeSpan.FromMinutes(1),
                _ =>
                {
                    calls++;
                    return Task.FromResult<Sample?>(null);
                });

            Assert.Null(first);
            Assert.Null(second);
            Assert.Equal(1, calls);
        }
        finally
        {
            await cache.RemoveAsync(key);
        }
    }

    [Fact]
    public async Task Remove_DeletesKey()
    {
        if (!TryGetLiveCache(out var cache))
        {
            return;
        }

        var key = CacheKey.For("test", "sample", Guid.NewGuid());
        await cache.SetAsync(key, new Sample(1, "a"), TimeSpan.FromMinutes(1));

        var removed = await cache.RemoveAsync(key);

        Assert.True(removed);
        Assert.Null(await cache.GetAsync<Sample>(key));
    }

    [Fact]
    public async Task GetOrSet_WhenRedisDown_FallsBackToFactory_WithoutThrowing()
    {
        var configuration = new ConfigurationOptions
        {
            EndPoints = { { "127.0.0.1", 6390 } },
            AbortOnConnectFail = false,
            ConnectTimeout = 200,
            ConnectRetry = 0,
        };
        using var broken = await ConnectionMultiplexer.ConnectAsync(configuration);
        var cache = new RedisCacheService(broken, NullLogger<RedisCacheService>.Instance);

        var called = false;
        var result = await cache.GetOrSetAsync(
            "hify:test:down",
            TimeSpan.FromMinutes(1),
            _ =>
            {
                called = true;
                return Task.FromResult(42);
            });

        Assert.True(called);
        Assert.Equal(42, result);
        // 值类型未命中/降级返回 default(int)=0（无法区分缺失与缓存的 0，符合约定）。
        Assert.Equal(0, await cache.GetAsync<int>("hify:test:down"));
    }
}
