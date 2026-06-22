using Hify.Modules.Conversation.Domain;
using Hify.Modules.Conversation.Features.Conversations;
using Hify.Modules.Conversation.Persistence;
using Hify.Modules.Conversation.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Conversation.Tests.Features.Conversations;

/// <summary>会话 CRUD 服务的真实库测试（连不上则跳过）。Agent 校验用内存替身。</summary>
public sealed class ConversationServiceTests : IAsyncLifetime
{
    private const long AgentId = 1;
    private const long ModelId = 1;

    private bool _available;

    public async Task InitializeAsync() => _available = await ConversationTestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static ConversationService NewService(ConversationDbContext db, FakeAgentQuery? agents = null) =>
        new(db, agents ?? new FakeAgentQuery().Add(FakeAgentQuery.ChatAgent(AgentId, ModelId)));

    [Fact]
    public async Task Create_AgentMissing_Returns4002()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var service = NewService(db, new FakeAgentQuery()); // 未预置 Agent

        var result = await service.CreateAsync(new CreateConversationRequest { AgentId = AgentId }, CancellationToken.None);

        Assert.Equal(4002, result.Code);
    }

    [Fact]
    public async Task Create_Valid_PersistsWithEmptyTitle()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        var result = await NewService(db).CreateAsync(new CreateConversationRequest { AgentId = AgentId }, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(AgentId, result.Data!.AgentId);
        Assert.Equal("", result.Data.Title);
        Assert.NotEqual(0, result.Data.Id);
    }

    [Fact]
    public async Task List_ReturnsNewestFirst()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var service = NewService(db);
        var first = await service.CreateAsync(new CreateConversationRequest { AgentId = AgentId }, CancellationToken.None);
        var second = await service.CreateAsync(new CreateConversationRequest { AgentId = AgentId }, CancellationToken.None);

        var page = await service.ListAsync(1, 20, CancellationToken.None);

        Assert.Equal(200, page.Code);
        var ids = page.Data!.Select(c => c.Id).ToList();
        Assert.True(ids.IndexOf(second.Data!.Id) < ids.IndexOf(first.Data!.Id), "更新的会话应排在前面");
    }

    [Fact]
    public async Task GetHistory_ConversationMissing_Returns4001()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        var page = await NewService(db).GetHistoryAsync(999_999_999, 1, 20, CancellationToken.None);

        Assert.Equal(4001, page.Code);
        Assert.Empty(page.Data!);
    }

    [Fact]
    public async Task GetHistory_ReturnsMessages()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var created = await NewService(db).CreateAsync(new CreateConversationRequest { AgentId = AgentId }, CancellationToken.None);
        var convId = created.Data!.Id;

        db.Messages.Add(new Message { ConversationId = convId, Role = MessageRoles.User, Content = "q", Status = MessageStatus.Completed });
        db.Messages.Add(new Message { ConversationId = convId, Role = MessageRoles.Assistant, Content = "a", Status = MessageStatus.Completed, ModelId = ModelId });
        await db.SaveChangesAsync();

        var page = await NewService(db).GetHistoryAsync(convId, 1, 20, CancellationToken.None);

        Assert.Equal(200, page.Code);
        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Data!.Count);
    }

    [Fact]
    public async Task Delete_CascadeSoftDeletes_ThenHistoryReturns4001()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var service = NewService(db);
        var created = await service.CreateAsync(new CreateConversationRequest { AgentId = AgentId }, CancellationToken.None);
        var convId = created.Data!.Id;
        db.Messages.Add(new Message { ConversationId = convId, Role = MessageRoles.User, Content = "q", Status = MessageStatus.Completed });
        await db.SaveChangesAsync();

        var deleted = await service.DeleteAsync(convId, CancellationToken.None);
        Assert.Equal(200, deleted.Code);

        Assert.Equal(4001, (await service.GetHistoryAsync(convId, 1, 20, CancellationToken.None)).Code);
        Assert.Equal(0, await db.Messages.CountAsync(m => m.ConversationId == convId));
    }

    [Fact]
    public async Task Delete_Missing_Returns4001()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        var result = await NewService(db).DeleteAsync(999_999_999, CancellationToken.None);

        Assert.Equal(4001, result.Code);
    }
}
