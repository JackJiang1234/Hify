using Hify.Modules.ModelProvider.Domain;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.ModelProvider.Tests.Persistence;

/// <summary>
/// 真实 PostgreSQL（docker-compose 的 db 服务）上的持久化行为测试：软删全局过滤、唯一约束、
/// 默认模型部分唯一索引、游标分页。连不上则静默跳过（与 Redis 集成测试一致，非 Mock）。
/// 每个用例在事务内执行且不提交，结束即回滚，保证对真实库零残留、互不干扰。
/// 前置：docker compose up -d（首次会自动应用 ddl.sql）。
/// </summary>
public sealed class ModelProviderPersistenceIntegrationTests : IAsyncLifetime
{
    // 默认对接 docker-compose 的本地 PG（5432，凭证同 compose 默认）；
    // 可用环境变量 HIFY_TEST_DB 覆盖连接串（如 CI 或本机 5432 已被占用时改端口）。连接/命令超时短，便于无库时快速跳过。
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("HIFY_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=hify;Username=hify;Password=hify;Timeout=3;Command Timeout=5";

    private bool _available;

    private sealed class FixedClock : IClock
    {
        // 非 0：软删需要写入非 0 的 deleted_at 才能被全局过滤命中。
        public long UtcNowEpochMs => 1_700_000_000_000;
    }

    private static ModelProviderDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<ModelProviderDbContext>().UseNpgsql(ConnectionString).Options,
            new FixedClock());

    public async Task InitializeAsync()
    {
        try
        {
            await using var context = NewContext();
            _available = await context.Database.CanConnectAsync();
        }
        catch
        {
            _available = false;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SoftDelete_SetsDeletedAt_AndIsFilteredOut()
    {
        if (!_available)
        {
            return;
        }

        await using var context = NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var provider = new Provider { Name = UniqueName(), ProviderType = "openai" };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();
        var id = provider.Id;

        context.Providers.Remove(provider);
        await context.SaveChangesAsync();

        Assert.Null(await context.Providers.FirstOrDefaultAsync(p => p.Id == id));

        var soft = await context.Providers.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
        Assert.NotNull(soft);
        Assert.NotEqual(0, soft!.DeletedAt);
    }

    [Fact]
    public async Task DuplicateProviderName_AmongLiveRows_ViolatesUniqueIndex()
    {
        if (!_available)
        {
            return;
        }

        await using var context = NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var name = UniqueName();
        context.Providers.Add(new Provider { Name = name, ProviderType = "openai" });
        await context.SaveChangesAsync();

        context.Providers.Add(new Provider { Name = name, ProviderType = "claude" });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SecondDefaultModel_SameProviderAndType_ViolatesPartialUniqueIndex()
    {
        if (!_available)
        {
            return;
        }

        await using var context = NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var provider = new Provider { Name = UniqueName(), ProviderType = "openai" };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        context.Models.Add(new Model
        {
            ProviderId = provider.Id,
            Name = "gpt-4o",
            ModelType = "chat",
            IsDefault = true,
        });
        await context.SaveChangesAsync();

        context.Models.Add(new Model
        {
            ProviderId = provider.Id,
            Name = "gpt-4o-mini",
            ModelType = "chat",
            IsDefault = true,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task CursorPagination_ReturnsRowsDescendingById_BelowCursor()
    {
        if (!_available)
        {
            return;
        }

        await using var context = NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var prefix = UniqueName();
        for (var i = 0; i < 5; i++)
        {
            context.Providers.Add(new Provider { Name = $"{prefix}-{i}", ProviderType = "openai" });
        }

        await context.SaveChangesAsync();

        var firstPage = await context.Providers
            .Where(p => p.Name.StartsWith(prefix))
            .OrderByDescending(p => p.Id)
            .Take(2)
            .ToListAsync();

        Assert.Equal(2, firstPage.Count);
        Assert.True(firstPage[0].Id > firstPage[1].Id);

        var cursor = firstPage[^1].Id;
        var secondPage = await context.Providers
            .Where(p => p.Name.StartsWith(prefix) && p.Id < cursor)
            .OrderByDescending(p => p.Id)
            .Take(2)
            .ToListAsync();

        Assert.Equal(2, secondPage.Count);
        Assert.True(secondPage[0].Id < cursor);
        Assert.True(secondPage[0].Id > secondPage[1].Id);
    }

    private static string UniqueName() => $"it-{Guid.NewGuid():N}";
}
