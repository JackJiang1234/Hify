using Hify.Modules.Workflow.Domain;
using Hify.Shared.Persistence;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Workflow.Persistence;

/// <summary>
/// Workflow 模块独立 DbContext（schema <c>workflow</c>），映射 workflow / workflow_run 两表。
/// 不启用 EF Migrations——表结构由仓库根 <c>ddl.sql</c> 手写维护；本类只负责映射与查询。
/// 软删过滤、snake_case 命名、审计时间戳由 <see cref="HifyDbContext"/> 基类统一处理。
/// </summary>
internal sealed class WorkflowDbContext : HifyDbContext
{
    /// <summary>构造。</summary>
    /// <param name="options">DbContext 选项。</param>
    /// <param name="clock">时间源，用于审计与软删时刻。</param>
    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options, IClock clock)
        : base(options, clock)
    {
    }

    /// <summary>工作流定义。</summary>
    public DbSet<Domain.Workflow> Workflows => Set<Domain.Workflow>();

    /// <summary>工作流执行记录。</summary>
    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 基类先应用 snake_case 命名、枚举→varchar、软删全局过滤。
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("workflow");

        // 索引与部分约束以 ddl.sql 为准（不启用 Migrations）；此处仅声明列长与列类型。
        ConfigureWorkflow(modelBuilder);
        ConfigureWorkflowRun(modelBuilder);
    }

    private static void ConfigureWorkflow(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Domain.Workflow>();
        entity.ToTable("workflow");
        entity.Property(w => w.Name).HasMaxLength(128);
        entity.Property(w => w.Description).HasMaxLength(512);
        entity.Property(w => w.Definition).HasColumnType("jsonb");
        entity.Property(w => w.Status).HasMaxLength(32);
    }

    private static void ConfigureWorkflowRun(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<WorkflowRun>();
        entity.ToTable("workflow_run");
        entity.Property(r => r.Status).HasMaxLength(32);
        entity.Property(r => r.Inputs).HasColumnType("jsonb");
        // output 为纯文本（end 节点产出），非 JSON——用 text 列，避免 jsonb 解析失败。
        entity.Property(r => r.Output).HasColumnType("text");
        entity.Property(r => r.Trace).HasColumnType("jsonb");
        entity.Property(r => r.ErrorMessage).HasMaxLength(512);
    }
}
