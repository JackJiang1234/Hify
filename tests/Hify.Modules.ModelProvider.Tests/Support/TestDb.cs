using Hify.Modules.ModelProvider.Persistence;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.ModelProvider.Tests.Support;

/// <summary>真实 PostgreSQL 测试辅助：连接串读 HIFY_TEST_DB（默认 5432），连不上则跳过。</summary>
internal static class TestDb
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("HIFY_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=hify;Username=hify;Password=hify;Timeout=3;Command Timeout=5";

    private sealed class FixedClock : IClock
    {
        public long UtcNowEpochMs => 1_700_000_000_000;
    }

    public static ModelProviderDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ModelProviderDbContext>().UseNpgsql(ConnectionString).Options, new FixedClock());

    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            await using var context = NewContext();
            return await context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }
}
