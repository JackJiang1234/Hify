using Hify.Modules.Conversation.Domain;
using Hify.Shared.Persistence;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Conversation.Persistence;

/// <summary>
/// Conversation 模块独立 DbContext（schema <c>conversation</c>），映射 conversation / message 两表。
/// 不启用 EF Migrations——表结构由仓库根 <c>ddl.sql</c> 手写维护；本类只负责映射与查询。
/// 软删过滤、snake_case 命名、审计时间戳由 <see cref="HifyDbContext"/> 基类统一处理。
/// </summary>
internal sealed class ConversationDbContext : HifyDbContext
{
    /// <summary>构造。</summary>
    /// <param name="options">DbContext 选项。</param>
    /// <param name="clock">时间源，用于审计与软删时刻。</param>
    public ConversationDbContext(DbContextOptions<ConversationDbContext> options, IClock clock)
        : base(options, clock)
    {
    }

    /// <summary>会话。</summary>
    public DbSet<Domain.Conversation> Conversations => Set<Domain.Conversation>();

    /// <summary>消息（增长最快的大表）。</summary>
    public DbSet<Message> Messages => Set<Message>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 基类先应用 snake_case 命名、枚举→varchar、软删全局过滤。
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("conversation");

        // 索引与部分约束以 ddl.sql 为准（不启用 Migrations）；此处仅声明列长与列类型。
        ConfigureConversation(modelBuilder);
        ConfigureMessage(modelBuilder);
    }

    private static void ConfigureConversation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Domain.Conversation>();
        entity.ToTable("conversation");
        entity.Property(c => c.Title).HasMaxLength(256);
    }

    private static void ConfigureMessage(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Message>();
        entity.ToTable("message");
        entity.Property(m => m.Role).HasMaxLength(32);
        entity.Property(m => m.ToolCalls).HasColumnType("jsonb");
        entity.Property(m => m.ToolCallId).HasMaxLength(64);
        entity.Property(m => m.FinishReason).HasMaxLength(32);
        entity.Property(m => m.Status).HasMaxLength(32);
        entity.Property(m => m.ErrorMessage).HasMaxLength(512);
    }
}
