using Hify.Modules.Conversation.Domain;
using Hify.Modules.Conversation.Persistence;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hify.Modules.Conversation.Tests.Persistence;

/// <summary>
/// DbContext 映射的离线断言（不连真实库）：验证两表落在 conversation schema、列名 snake_case、
/// tool_calls 为 jsonb。落库行为（软删过滤、按 id 排序）在真实库集成测试中验证。
/// </summary>
public sealed class ConversationDbContextMappingTests
{
    private sealed class FixedClock : IClock
    {
        public long UtcNowEpochMs => 0;
    }

    private static ConversationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ConversationDbContext>()
            .UseNpgsql("Host=localhost;Database=hify;Username=hify;Password=placeholder")
            .Options;
        return new ConversationDbContext(options, new FixedClock());
    }

    [Theory]
    [InlineData(typeof(Domain.Conversation), "conversation")]
    [InlineData(typeof(Message), "message")]
    public void Entity_MapsToConversationSchema_WithSnakeCaseTable(Type clrType, string expectedTable)
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(clrType);

        Assert.NotNull(entityType);
        Assert.Equal("conversation", entityType!.GetSchema());
        Assert.Equal(expectedTable, entityType.GetTableName());
    }

    [Theory]
    [InlineData(nameof(Message.ConversationId), "conversation_id")]
    [InlineData(nameof(Message.ToolCalls), "tool_calls")]
    [InlineData(nameof(Message.ToolCallId), "tool_call_id")]
    [InlineData(nameof(Message.FinishReason), "finish_reason")]
    [InlineData(nameof(Message.ErrorMessage), "error_message")]
    [InlineData(nameof(Message.ModelId), "model_id")]
    [InlineData(nameof(Message.PromptTokens), "prompt_tokens")]
    [InlineData(nameof(Message.CompletionTokens), "completion_tokens")]
    public void MessageColumns_AreSnakeCased(string propertyName, string expectedColumn)
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Message))!;
        var store = StoreObjectIdentifier.Table("message", "conversation");

        var column = entityType.FindProperty(propertyName)!.GetColumnName(store);

        Assert.Equal(expectedColumn, column);
    }

    [Fact]
    public void MessageToolCalls_IsJsonbColumn()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Message))!;

        var columnType = entityType.FindProperty(nameof(Message.ToolCalls))!.GetColumnType();

        Assert.Equal("jsonb", columnType);
    }

    [Fact]
    public void ConversationColumns_AreSnakeCased()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Domain.Conversation))!;
        var store = StoreObjectIdentifier.Table("conversation", "conversation");

        Assert.Equal("agent_id", entityType.FindProperty(nameof(Domain.Conversation.AgentId))!.GetColumnName(store));
        Assert.Equal("title", entityType.FindProperty(nameof(Domain.Conversation.Title))!.GetColumnName(store));
    }
}
