using Hify.Modules.Workflow.Persistence;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Workflow.Tests.Support;

/// <summary>真实 PostgreSQL 测试辅助：连接串读 HIFY_TEST_DB（默认 5432），连不上则跳过。</summary>
internal static class WorkflowTestDb
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("HIFY_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=hify;Username=hify;Password=hify;Timeout=3;Command Timeout=5";

    /// <summary>固定时钟：非 0，软删需写入非 0 deleted_at 才能被全局过滤命中。</summary>
    public sealed class FixedClock : IClock
    {
        public long UtcNowEpochMs => 1_700_000_000_000;
    }

    public static WorkflowDbContext NewContext() =>
        new(new DbContextOptionsBuilder<WorkflowDbContext>().UseNpgsql(ConnectionString).Options, new FixedClock());

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
