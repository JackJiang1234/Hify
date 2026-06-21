using Hify.Modules.Agent.Domain;
using Hify.Modules.Agent.Persistence;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hify.Modules.Agent.Tests.Persistence;

/// <summary>
/// DbContext 映射的离线断言（不连真实库）：验证三表落在 agent schema、列名 snake_case、
/// 参数列为 jsonb。落库行为（软删过滤、唯一约束、级联软删）在真实库集成测试中验证。
/// </summary>
public sealed class AgentDbContextMappingTests
{
    private sealed class FixedClock : IClock
    {
        public long UtcNowEpochMs => 0;
    }

    private static AgentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AgentDbContext>()
            .UseNpgsql("Host=localhost;Database=hify;Username=hify;Password=placeholder")
            .Options;
        return new AgentDbContext(options, new FixedClock());
    }

    [Theory]
    [InlineData(typeof(Domain.Agent), "agent")]
    [InlineData(typeof(AgentTool), "agent_tool")]
    [InlineData(typeof(AgentKnowledge), "agent_knowledge")]
    public void Entity_MapsToAgentSchema_WithSnakeCaseTable(Type clrType, string expectedTable)
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(clrType);

        Assert.NotNull(entityType);
        Assert.Equal("agent", entityType!.GetSchema());
        Assert.Equal(expectedTable, entityType.GetTableName());
    }

    [Theory]
    [InlineData(nameof(Domain.Agent.ModelId), "model_id")]
    [InlineData(nameof(Domain.Agent.SystemPrompt), "system_prompt")]
    [InlineData(nameof(Domain.Agent.ModelParams), "model_params")]
    [InlineData(nameof(Domain.Agent.RetrievalParams), "retrieval_params")]
    [InlineData(nameof(Domain.Agent.MaxIterations), "max_iterations")]
    public void AgentColumns_AreSnakeCased(string propertyName, string expectedColumn)
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Domain.Agent))!;
        var store = StoreObjectIdentifier.Table("agent", "agent");

        var column = entityType.FindProperty(propertyName)!.GetColumnName(store);

        Assert.Equal(expectedColumn, column);
    }

    [Theory]
    [InlineData(nameof(Domain.Agent.ModelParams))]
    [InlineData(nameof(Domain.Agent.RetrievalParams))]
    public void AgentParams_AreJsonbColumns(string propertyName)
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Domain.Agent))!;

        var columnType = entityType.FindProperty(propertyName)!.GetColumnType();

        Assert.Equal("jsonb", columnType);
    }

    [Fact]
    public void AgentToolColumns_AreSnakeCased()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(AgentTool))!;
        var store = StoreObjectIdentifier.Table("agent_tool", "agent");

        Assert.Equal("agent_id", entityType.FindProperty(nameof(AgentTool.AgentId))!.GetColumnName(store));
        Assert.Equal("tool_id", entityType.FindProperty(nameof(AgentTool.ToolId))!.GetColumnName(store));
    }
}
