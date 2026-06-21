using Hify.Modules.Agent.Domain;
using Hify.Shared.Persistence;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Agent.Persistence;

/// <summary>
/// Agent 模块独立 DbContext（schema <c>agent</c>），映射 agent / agent_tool / agent_knowledge 三表。
/// 不启用 EF Migrations——表结构由仓库根 <c>ddl.sql</c> 手写维护；本类只负责映射与查询。
/// 软删过滤、snake_case 命名、审计时间戳由 <see cref="HifyDbContext"/> 基类统一处理。
/// </summary>
internal sealed class AgentDbContext : HifyDbContext
{
    /// <summary>构造。</summary>
    /// <param name="options">DbContext 选项。</param>
    /// <param name="clock">时间源，用于审计与软删时刻。</param>
    public AgentDbContext(DbContextOptions<AgentDbContext> options, IClock clock)
        : base(options, clock)
    {
    }

    /// <summary>Agent 配置。</summary>
    public DbSet<Domain.Agent> Agents => Set<Domain.Agent>();

    /// <summary>Agent ↔ MCP 工具绑定。</summary>
    public DbSet<AgentTool> AgentTools => Set<AgentTool>();

    /// <summary>Agent ↔ 知识库绑定。</summary>
    public DbSet<AgentKnowledge> AgentKnowledges => Set<AgentKnowledge>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 基类先应用 snake_case 命名、枚举→varchar、软删全局过滤。
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("agent");

        // 索引与唯一/部分约束以 ddl.sql 为准（不启用 Migrations）；此处仅声明列长与列类型。
        ConfigureAgent(modelBuilder);
        ConfigureAgentTool(modelBuilder);
        ConfigureAgentKnowledge(modelBuilder);
    }

    private static void ConfigureAgent(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Domain.Agent>();
        entity.ToTable("agent");
        entity.Property(a => a.Name).HasMaxLength(128);
        entity.Property(a => a.Description).HasMaxLength(512);
        entity.Property(a => a.ModelParams).HasColumnType("jsonb");
        entity.Property(a => a.RetrievalParams).HasColumnType("jsonb");
    }

    private static void ConfigureAgentTool(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentTool>().ToTable("agent_tool");
    }

    private static void ConfigureAgentKnowledge(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentKnowledge>().ToTable("agent_knowledge");
    }
}
