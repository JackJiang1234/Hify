using Hify.Modules.Conversation.Domain;
using Hify.Modules.Conversation.Features.Chat;
using Hify.Modules.Conversation.Features.Context;
using Hify.Modules.Conversation.Features.Retrieval;
using Hify.Modules.Conversation.Persistence;
using Hify.Modules.Conversation.Tests.Support;

namespace Hify.Modules.Conversation.Tests.Features.Chat;

/// <summary>
/// ContextBuilder 的真实库测试（历史取自 PG；Agent/Model 用内存替身）。连不上则跳过。
/// 覆盖：引用校验失败码、上下文超窗、正常装配（system 在前、user 在末、历史按预算裁剪）。
/// </summary>
public sealed class ContextBuilderTests : IAsyncLifetime
{
    private const long AgentId = 1;
    private const long ModelId = 1;

    private bool _available;

    public async Task InitializeAsync() => _available = await ConversationTestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static ContextBuilder NewBuilder(
        ConversationDbContext db,
        FakeAgentQuery? agents = null,
        FakeModelProviderQuery? models = null) =>
        new(
            db,
            agents ?? new FakeAgentQuery().Add(FakeAgentQuery.ChatAgent(AgentId, ModelId)),
            models ?? new FakeModelProviderQuery().Add(FakeModelProviderQuery.ChatModel(ModelId)),
            new NoopRetriever(),
            new CharBasedTokenEstimator(),
            new ConversationContextCache(new PassthroughCacheService()));

    private static async Task<long> SeedConversationAsync(ConversationDbContext db, params (string Role, string Content)[] history)
    {
        var conversation = new Domain.Conversation { AgentId = AgentId };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();

        foreach (var (role, content) in history)
        {
            db.Messages.Add(new Message
            {
                ConversationId = conversation.Id,
                Role = role,
                Content = content,
                Status = MessageStatus.Completed,
            });
        }

        await db.SaveChangesAsync();
        return conversation.Id;
    }

    [Fact]
    public async Task Build_AgentMissing_Returns4002()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db);
        var builder = NewBuilder(db, agents: new FakeAgentQuery()); // 未预置 Agent

        var result = await builder.BuildAsync(convId, AgentId, "hi", CancellationToken.None);

        Assert.Equal(4002, result.Code);
    }

    [Fact]
    public async Task Build_AgentDisabled_Returns4002()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db);
        var agents = new FakeAgentQuery().Add(FakeAgentQuery.ChatAgent(AgentId, ModelId, enabled: false));

        var result = await NewBuilder(db, agents: agents).BuildAsync(convId, AgentId, "hi", CancellationToken.None);

        Assert.Equal(4002, result.Code);
    }

    [Fact]
    public async Task Build_ModelMissing_Returns4003()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db);
        var builder = NewBuilder(db, models: new FakeModelProviderQuery()); // 未预置模型

        var result = await builder.BuildAsync(convId, AgentId, "hi", CancellationToken.None);

        Assert.Equal(4003, result.Code);
    }

    [Fact]
    public async Task Build_ContextWindowTooSmall_Returns4007()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db);
        // 窗口极小，扣掉 maxOutput + 余量后为负。
        var models = new FakeModelProviderQuery().Add(FakeModelProviderQuery.ChatModel(ModelId, contextWindow: 10, maxOutputTokens: 8));

        var result = await NewBuilder(db, models: models).BuildAsync(convId, AgentId, "hello", CancellationToken.None);

        Assert.Equal(4007, result.Code);
    }

    [Fact]
    public async Task Build_Normal_AssemblesSystemFirstUserLastWithHistory()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db, ("user", "previous question"), ("assistant", "previous answer"));

        var result = await NewBuilder(db).BuildAsync(convId, AgentId, "new question", CancellationToken.None);

        Assert.Equal(200, result.Code);
        var messages = result.Data!.Request.Messages;
        Assert.Equal(ModelId, result.Data.ModelId);
        Assert.Equal(MessageRoles.System, messages[0].Role);
        Assert.Equal("you are helpful", messages[0].Content);
        Assert.Equal(MessageRoles.User, messages[^1].Role);
        Assert.Equal("new question", messages[^1].Content);
        // system + 2 条历史 + 本次输入。
        Assert.Equal(4, messages.Count);
        Assert.Contains(messages, m => m.Content == "previous question");
        Assert.Contains(messages, m => m.Content == "previous answer");
    }

    [Fact]
    public async Task Build_TightWindow_DropsOldHistoryButKeepsInput()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db, ("user", new string('a', 300)), ("assistant", new string('b', 300)));
        // 窗口只够 system + 本次输入 + 余量，几乎不留给历史。
        var models = new FakeModelProviderQuery().Add(FakeModelProviderQuery.ChatModel(ModelId, contextWindow: 400, maxOutputTokens: 64));

        var result = await NewBuilder(db, models: models).BuildAsync(convId, AgentId, "short", CancellationToken.None);

        Assert.Equal(200, result.Code);
        var messages = result.Data!.Request.Messages;
        Assert.Equal(MessageRoles.User, messages[^1].Role);
        Assert.Equal("short", messages[^1].Content);
        // 长历史被裁掉。
        Assert.DoesNotContain(messages, m => m.Content.Length == 300);
    }
}
