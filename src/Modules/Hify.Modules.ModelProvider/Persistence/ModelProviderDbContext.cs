using Hify.Modules.ModelProvider.Domain;
using Hify.Shared.Persistence;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.ModelProvider.Persistence;

/// <summary>
/// ModelProvider 模块独立 DbContext（schema <c>model_provider</c>），映射 provider / model / provider_health 三表。
/// 不启用 EF Migrations——表结构由仓库根 <c>ddl.sql</c> 手写维护；本类只负责映射与查询。
/// 软删过滤、snake_case 命名、审计时间戳由 <see cref="HifyDbContext"/> 基类统一处理。
/// </summary>
internal sealed class ModelProviderDbContext : HifyDbContext
{
    /// <summary>构造。</summary>
    /// <param name="options">DbContext 选项。</param>
    /// <param name="clock">时间源，用于审计与软删时刻。</param>
    public ModelProviderDbContext(DbContextOptions<ModelProviderDbContext> options, IClock clock)
        : base(options, clock)
    {
    }

    /// <summary>供应商实例。</summary>
    public DbSet<Provider> Providers => Set<Provider>();

    /// <summary>供应商下的模型。</summary>
    public DbSet<Model> Models => Set<Model>();

    /// <summary>供应商健康（与供应商 1:1）。</summary>
    public DbSet<ProviderHealth> ProviderHealths => Set<ProviderHealth>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 基类先应用 snake_case 命名、枚举→varchar、软删全局过滤。
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("model_provider");

        // 索引与唯一/部分约束以 ddl.sql 为准（不启用 Migrations）；此处仅声明列长与列类型。
        ConfigureProvider(modelBuilder);
        ConfigureModel(modelBuilder);
        ConfigureProviderHealth(modelBuilder);
    }

    private static void ConfigureProvider(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Provider>();
        entity.ToTable("provider");
        entity.Property(p => p.Name).HasMaxLength(128);
        entity.Property(p => p.ProviderType).HasMaxLength(32);
        entity.Property(p => p.BaseUrl).HasMaxLength(512);
        entity.Property(p => p.AuthType).HasMaxLength(32);
        entity.Property(p => p.AuthHeaderName).HasMaxLength(64);
        entity.Property(p => p.ApiKeyCipher).HasMaxLength(1024);
        entity.Property(p => p.ApiKeyHint).HasMaxLength(16);
        entity.Property(p => p.Settings).HasColumnType("jsonb");
    }

    private static void ConfigureModel(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Model>();
        entity.ToTable("model");
        entity.Property(m => m.Name).HasMaxLength(128);
        entity.Property(m => m.DisplayName).HasMaxLength(128);
        entity.Property(m => m.ModelType).HasMaxLength(32);
        entity.Property(m => m.Source).HasMaxLength(32);
    }

    private static void ConfigureProviderHealth(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProviderHealth>();
        entity.ToTable("provider_health");
        entity.Property(h => h.Status).HasMaxLength(32);
        entity.Property(h => h.LastError).HasMaxLength(512);
    }
}
