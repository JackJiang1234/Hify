using Hify.Modules.Conversation.Domain;
using Hify.Modules.Conversation.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Conversation.Tests.Persistence;

/// <summary>
/// 真实 PostgreSQL 上的持久化行为测试：审计时间戳、软删全局过滤、按 id 读历史。
/// 连不上则静默跳过；每个用例在事务内执行且不提交，结束即回滚，保证对真实库零残留。
/// 前置：docker compose up -d（首次会自动应用 ddl.sql）。
/// </summary>
public sealed class ConversationPersistenceIntegrationTests : IAsyncLifetime
{
    private bool _available;

    public async Task InitializeAsync() => _available = await ConversationTestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddConversation_FillsAuditTimestamps()
    {
        if (!_available)
        {
            return;
        }

        await using var context = ConversationTestDb.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var conversation = new Domain.Conversation { AgentId = 1, Title = "" };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        Assert.NotEqual(0, conversation.Id);
        Assert.NotEqual(0, conversation.CreatedAt);
        Assert.NotEqual(0, conversation.UpdatedAt);
        Assert.Equal(0, conversation.DeletedAt);
    }

    [Fact]
    public async Task SoftDeleteConversation_IsFilteredOut()
    {
        if (!_available)
        {
            return;
        }

        await using var context = ConversationTestDb.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var conversation = new Domain.Conversation { AgentId = 1 };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();
        var id = conversation.Id;

        context.Conversations.Remove(conversation);
        await context.SaveChangesAsync();

        Assert.Null(await context.Conversations.FirstOrDefaultAsync(c => c.Id == id));
        var soft = await context.Conversations.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
        Assert.NotNull(soft);
        Assert.NotEqual(0, soft!.DeletedAt);
    }

    [Fact]
    public async Task Messages_ReadInInsertionOrderById()
    {
        if (!_available)
        {
            return;
        }

        await using var context = ConversationTestDb.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var conversation = new Domain.Conversation { AgentId = 1 };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        context.Messages.Add(new Message { ConversationId = conversation.Id, Role = "user", Content = "hi", Status = "completed" });
        context.Messages.Add(new Message { ConversationId = conversation.Id, Role = "assistant", Content = "hello", Status = "completed", ModelId = 1, CompletionTokens = 3 });
        await context.SaveChangesAsync();

        var history = await context.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id)
            .OrderBy(m => m.Id)
            .ToListAsync();

        Assert.Equal(2, history.Count);
        Assert.Equal("user", history[0].Role);
        Assert.Equal("assistant", history[1].Role);
        Assert.Equal("[]", history[1].ToolCalls);
    }

    [Fact]
    public async Task Message_JsonbToolCalls_RoundTrips()
    {
        if (!_available)
        {
            return;
        }

        await using var context = ConversationTestDb.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var conversation = new Domain.Conversation { AgentId = 1 };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var message = new Message
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = "",
            ToolCalls = """[{"id":"call_1","name":"search","arguments":"{}"}]""",
            Status = "completed",
        };
        context.Messages.Add(message);
        await context.SaveChangesAsync();

        // 在同一事务连接上重查（AsNoTracking 强制走库，真实 jsonb 往返）；
        // 事务结束回滚保证零残留——不可另开连接，否则读不到未提交行。
        var fetched = await context.Messages.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        Assert.Contains("call_1", fetched.ToolCalls);
    }
}
