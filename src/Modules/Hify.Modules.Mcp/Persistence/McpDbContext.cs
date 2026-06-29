using Hify.Modules.Mcp.Domain;
using Hify.Shared.Persistence;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Mcp.Persistence;

/// <summary>
/// MCP 模块独立 DbContext（schema <c>mcp</c>），映射 mcp_server / mcp_tool 两表。
/// 不启用 EF Migrations——表结构由仓库根 <c>ddl.sql</c> 手写维护；本类只负责映射与查询。
/// 软删过滤、snake_case 命名、审计时间戳由 <see cref="HifyDbContext"/> 基类统一处理。
/// </summary>
internal sealed class McpDbContext : HifyDbContext
{
    /// <summary>构造。</summary>
    /// <param name="options">DbContext 选项。</param>
    /// <param name="clock">时间源，用于审计与软删时刻。</param>
    public McpDbContext(DbContextOptions<McpDbContext> options, IClock clock)
        : base(options, clock)
    {
    }

    /// <summary>MCP Server 接入配置。</summary>
    public DbSet<McpServer> McpServers => Set<McpServer>();

    /// <summary>从 Server 发现的工具。</summary>
    public DbSet<McpTool> McpTools => Set<McpTool>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 基类先应用 snake_case 命名、枚举→varchar、软删全局过滤。
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("mcp");

        // 索引与唯一/部分约束以 ddl.sql 为准（不启用 Migrations）；此处仅声明列长与列类型。
        ConfigureMcpServer(modelBuilder);
        ConfigureMcpTool(modelBuilder);
    }

    private static void ConfigureMcpServer(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<McpServer>();
        entity.ToTable("mcp_server");
        entity.Property(s => s.Name).HasMaxLength(128);
        entity.Property(s => s.Transport).HasMaxLength(32);
        entity.Property(s => s.Endpoint).HasMaxLength(512);
        entity.Property(s => s.AuthType).HasMaxLength(32);
        entity.Property(s => s.AuthHeaderName).HasMaxLength(64);
        entity.Property(s => s.ApiKeyCipher).HasMaxLength(1024);
        entity.Property(s => s.ApiKeyHint).HasMaxLength(16);
        entity.Property(s => s.LastError).HasMaxLength(512);
    }

    private static void ConfigureMcpTool(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<McpTool>();
        entity.ToTable("mcp_tool");
        entity.Property(t => t.Name).HasMaxLength(128);
        entity.Property(t => t.Description).HasColumnType("text");
        entity.Property(t => t.InputSchema).HasColumnType("jsonb");
    }
}
