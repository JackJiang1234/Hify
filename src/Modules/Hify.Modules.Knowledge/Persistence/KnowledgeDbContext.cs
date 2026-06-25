using Hify.Modules.Knowledge.Domain;
using Hify.Shared.Persistence;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Knowledge.Persistence;

/// <summary>
/// Knowledge 模块独立 DbContext（schema <c>knowledge</c>）。映射 knowledge_base / document / chunk（含 1536 维向量）。
/// 不启用 EF Migrations——表结构由仓库根 <c>ddl.sql</c> 手写维护（含 HNSW 向量索引）。
/// 软删过滤、snake_case 命名、审计时间戳由 <see cref="HifyDbContext"/> 基类统一处理。
/// 注：向量类型映射经 <c>UseNpgsql(..., o =&gt; o.UseVector())</c> 在 DbContext 选项处启用。
/// </summary>
internal sealed class KnowledgeDbContext : HifyDbContext
{
    private readonly IClock _clock;

    /// <summary>构造。</summary>
    /// <param name="options">DbContext 选项。</param>
    /// <param name="clock">时间源，用于审计与软删时刻。</param>
    public KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options, IClock clock)
        : base(options, clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// 时间源（epoch ms）。供需要绕过 SaveChanges 软删拦截的批量操作（如级联删除用 ExecuteUpdate）
    /// 取统一删除时刻，与基类审计同源。
    /// </summary>
    internal IClock Clock => _clock;

    /// <summary>知识库。</summary>
    public DbSet<KnowledgeBase> KnowledgeBases => Set<KnowledgeBase>();

    /// <summary>文档。</summary>
    public DbSet<Document> Documents => Set<Document>();

    /// <summary>文档分块 + 向量。</summary>
    public DbSet<Chunk> Chunks => Set<Chunk>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 基类先应用 snake_case 命名、枚举→varchar、软删全局过滤。
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("knowledge");

        // 索引与唯一/部分约束以 ddl.sql 为准（不启用 Migrations）；此处仅声明列长与向量列类型。
        ConfigureKnowledgeBase(modelBuilder);
        ConfigureDocument(modelBuilder);
        ConfigureChunk(modelBuilder);
    }

    private static void ConfigureKnowledgeBase(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<KnowledgeBase>();
        entity.ToTable("knowledge_base");
        entity.Property(kb => kb.Name).HasMaxLength(128);
        entity.Property(kb => kb.Description).HasMaxLength(512);
    }

    private static void ConfigureDocument(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Document>();
        entity.ToTable("document");
        entity.Property(document => document.Name).HasMaxLength(256);
        entity.Property(document => document.FileType).HasMaxLength(32);
        entity.Property(document => document.ContentHash).HasMaxLength(64);
        entity.Property(document => document.Status).HasMaxLength(32);
        entity.Property(document => document.ErrorMessage).HasMaxLength(512);
    }

    private static void ConfigureChunk(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Chunk>();
        entity.ToTable("chunk");
        // Content 为 text（不设长度）；Embedding 固定 1536 维，与 ddl.sql 的 HNSW 索引对齐。
        entity.Property(chunk => chunk.Embedding).HasColumnType("vector(1536)");
    }
}
