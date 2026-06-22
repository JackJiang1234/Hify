using Hify.Contracts.ModelProvider;
using Hify.Modules.Conversation.Domain;
using Hify.Modules.Conversation.Features.Chat;
using Hify.Modules.Conversation.Features.Context;
using Hify.Modules.Conversation.Features.Retrieval;
using Hify.Modules.Conversation.Persistence;
using Hify.Modules.Conversation.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Conversation.Tests.Features.Chat;

/// <summary>
/// ConversationOrchestrator 的真实库测试（持久化用 PG，Agent/Model/LLM 用替身）。连不上则跳过。
/// 覆盖：准备阶段错误码、正常流式 + 落库 + 标题回填、中途失败落 failed、取消落 cancelled、多轮历史。
/// </summary>
public sealed class ConversationOrchestratorTests : IAsyncLifetime
{
    private const long AgentId = 1;
    private const long ModelId = 1;

    private bool _available;

    public async Task InitializeAsync() => _available = await ConversationTestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static ConversationContextCache NewCache() => new(new PassthroughCacheService());

    private static ContextBuilder NewContextBuilder(ConversationDbContext db) =>
        new(
            db,
            new FakeAgentQuery().Add(FakeAgentQuery.ChatAgent(AgentId, ModelId)),
            new FakeModelProviderQuery().Add(FakeModelProviderQuery.ChatModel(ModelId)),
            new NoopRetriever(),
            new CharBasedTokenEstimator(),
            NewCache());

    private static ConversationOrchestrator NewOrchestrator(ConversationDbContext db, IModelInvoker invoker) =>
        new(db, NewContextBuilder(db), invoker, NewCache());

    private static async Task<long> SeedConversationAsync(ConversationDbContext db, string title = "")
    {
        var conversation = new Domain.Conversation { AgentId = AgentId, Title = title };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        return conversation.Id;
    }

    private static async Task<List<ChatEvent>> DrainAsync(ConversationOrchestrator orchestrator, ChatSession session, CancellationToken ct)
    {
        var events = new List<ChatEvent>();
        await foreach (var ev in orchestrator.StreamAsync(session, ct))
        {
            events.Add(ev);
        }

        return events;
    }

    [Fact]
    public async Task Prepare_ConversationMissing_Returns4001()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        var orchestrator = NewOrchestrator(db, FakeModelInvoker.Streaming("hi"));

        var result = await orchestrator.PrepareAsync(999_999_999, "hello", CancellationToken.None);

        Assert.Equal(4001, result.Code);
    }

    [Fact]
    public async Task Prepare_UpstreamInitialFailure_Returns4005()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db);
        var orchestrator = NewOrchestrator(db, FakeModelInvoker.InitialFailure());

        var result = await orchestrator.PrepareAsync(convId, "hello", CancellationToken.None);

        Assert.Equal(4005, result.Code);
    }

    [Fact]
    public async Task StreamReply_Success_YieldsDeltasThenDone_AndPersists()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db);
        var orchestrator = NewOrchestrator(db, FakeModelInvoker.Streaming("Hel", "lo", "!"));

        var prepared = await orchestrator.PrepareAsync(convId, "hi there", CancellationToken.None);
        Assert.Equal(200, prepared.Code);

        var events = await DrainAsync(orchestrator, prepared.Data!, CancellationToken.None);

        var deltas = events.Where(e => e.Type == ChatEventType.Delta).Select(e => e.Text).ToList();
        Assert.Equal(new[] { "Hel", "lo", "!" }, deltas);

        var done = Assert.Single(events, e => e.Type == ChatEventType.Done);
        Assert.Equal("stop", done.FinishReason);
        Assert.Equal(7, done.CompletionTokens);
        Assert.NotEqual(0, done.MessageId);

        // 落库：user + assistant 两条，assistant 内容拼接完整、completed。
        var messages = await db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == convId).OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Equal(MessageRoles.User, messages[0].Role);
        Assert.Equal("hi there", messages[0].Content);
        Assert.Equal(MessageRoles.Assistant, messages[1].Role);
        Assert.Equal("Hello!", messages[1].Content);
        Assert.Equal(MessageStatus.Completed, messages[1].Status);
        Assert.Equal(ModelId, messages[1].ModelId);
    }

    [Fact]
    public async Task StreamReply_FirstMessage_BackfillsTitle()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db, title: "");
        var orchestrator = NewOrchestrator(db, FakeModelInvoker.Streaming("ok"));

        var prepared = await orchestrator.PrepareAsync(convId, "这是第一条用户消息", CancellationToken.None);
        await DrainAsync(orchestrator, prepared.Data!, CancellationToken.None);

        var conversation = await db.Conversations.AsNoTracking().FirstAsync(c => c.Id == convId);
        Assert.Equal("这是第一条用户消息", conversation.Title);
    }

    [Fact]
    public async Task StreamReply_MidStreamError_YieldsError_PersistsFailed()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db);
        var orchestrator = NewOrchestrator(db, FakeModelInvoker.ThrowsMidStream("partial "));

        var prepared = await orchestrator.PrepareAsync(convId, "hi", CancellationToken.None);
        var events = await DrainAsync(orchestrator, prepared.Data!, CancellationToken.None);

        Assert.Contains(events, e => e.Type == ChatEventType.Delta && e.Text == "partial ");
        var error = Assert.Single(events, e => e.Type == ChatEventType.Error);
        Assert.Equal(4005, error.ErrorCode);
        Assert.DoesNotContain(events, e => e.Type == ChatEventType.Done);

        var assistant = await db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == convId && m.Role == MessageRoles.Assistant).SingleAsync();
        Assert.Equal(MessageStatus.Failed, assistant.Status);
        Assert.Equal("partial ", assistant.Content);
        Assert.Equal("error", assistant.FinishReason);
    }

    [Fact]
    public async Task StreamReply_Cancelled_PersistsCancelled()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db);
        var orchestrator = NewOrchestrator(db, FakeModelInvoker.Streaming("a", "b", "c", "d"));

        var prepared = await orchestrator.PrepareAsync(convId, "hi", CancellationToken.None);

        using var cts = new CancellationTokenSource();
        var events = new List<ChatEvent>();
        await foreach (var ev in orchestrator.StreamAsync(prepared.Data!, cts.Token))
        {
            events.Add(ev);
            cts.Cancel(); // 收到首片后立即取消
        }

        Assert.DoesNotContain(events, e => e.Type == ChatEventType.Done);
        var assistant = await db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == convId && m.Role == MessageRoles.Assistant).SingleAsync();
        Assert.Equal(MessageStatus.Cancelled, assistant.Status);
    }

    [Fact]
    public async Task StreamReply_SecondTurn_IncludesPriorHistory()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db);

        // 第一轮。
        var first = await orchestratorTurnAsync(db, convId, "first question", "first answer");
        Assert.Equal(200, first.Code);

        // 第二轮：用记录调用消息的替身，断言请求里带了上一轮历史。
        var recordingInvoker = new RecordingInvoker();
        var orchestrator = new ConversationOrchestrator(db, NewContextBuilder(db), recordingInvoker, NewCache());
        var prepared = await orchestrator.PrepareAsync(convId, "second question", CancellationToken.None);
        await DrainAsync(orchestrator, prepared.Data!, CancellationToken.None);

        var sentContents = recordingInvoker.LastRequest!.Messages.Select(m => m.Content).ToList();
        Assert.Contains("first question", sentContents);
        Assert.Contains("first answer", sentContents);
        Assert.Equal("second question", sentContents[^1]);
    }

    private async Task<Hify.Shared.Results.Result<ChatSession>> orchestratorTurnAsync(
        ConversationDbContext db, long convId, string input, string answer)
    {
        var orchestrator = NewOrchestrator(db, FakeModelInvoker.Streaming(answer));
        var prepared = await orchestrator.PrepareAsync(convId, input, CancellationToken.None);
        if (prepared.Code == 200)
        {
            await DrainAsync(orchestrator, prepared.Data!, CancellationToken.None);
        }

        return prepared;
    }

    /// <summary>记录最后一次收到的 ChatRequest，用于断言历史拼接。</summary>
    private sealed class RecordingInvoker : IModelInvoker
    {
        public ChatRequest? LastRequest { get; private set; }

        public Task<Hify.Shared.Results.Result<ChatResponse>> ChatAsync(long modelId, ChatRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Hify.Shared.Results.Result<IAsyncEnumerable<ChatStreamChunk>>> ChatStreamAsync(long modelId, ChatRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Hify.Shared.Results.Result<IAsyncEnumerable<ChatStreamChunk>>.Ok(Single()));
        }

        public Task<Hify.Shared.Results.Result<EmbeddingResponse>> EmbedAsync(long modelId, EmbeddingRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        private static async IAsyncEnumerable<ChatStreamChunk> Single()
        {
            yield return new ChatStreamChunk { Delta = "second answer" };
            await Task.Yield();
            yield return new ChatStreamChunk { IsFinal = true, FinishReason = "stop" };
        }
    }
}
