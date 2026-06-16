using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;

namespace Hify.Shared.Persistence;

/// <summary>
/// 各模块 DbContext 的基类。统一落地数据库强制约定：
/// 应用 Hify 模型约定（snake_case、枚举存 varchar、软删过滤）；保存时自动填充审计时间戳；
/// 将物理删除转为软删除（置 <c>deleted_at</c>）。每模块继承并维护各自独立 schema。
/// </summary>
public abstract class HifyDbContext : DbContext
{
    private readonly IClock _clock;

    /// <summary>构造基类。</summary>
    /// <param name="options">DbContext 选项。</param>
    /// <param name="clock">时间源，用于审计与软删时刻。</param>
    protected HifyDbContext(DbContextOptions options, IClock clock)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyHifyConventions();
    }

    /// <inheritdoc />
    public override int SaveChanges()
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditAndSoftDelete()
    {
        var now = _clock.UtcNowEpochMs;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedAt == 0)
                    {
                        entry.Entity.CreatedAt = now;
                    }

                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Deleted:
                    // 物理删除转软删除：标记删除时刻而非真正移除行。
                    entry.State = EntityState.Modified;
                    entry.Entity.DeletedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;

                default:
                    break;
            }
        }
    }
}
