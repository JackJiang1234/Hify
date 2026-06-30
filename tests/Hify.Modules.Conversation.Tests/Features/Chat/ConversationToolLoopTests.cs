using Hify.Contracts.Mcp;
using Hify.Contracts.ModelProvider;
using Hify.Modules.Conversation.Domain;
using Hify.Modules.Conversation.Features.Chat;
using Hify.Modules.Conversation.Features.Context;
using Hify.Modules.Conversation.Features.Retrieval;
using Hify.Modules.Conversation.Persistence;
using Hify.Modules.Conversation.Tests.Support;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Conversation.Tests.Features.Chat;

/// <summary>
/// 工具循环：模型调用工具→执行并回喂→收尾流式最终答；落库 assistant(tool_calls)+tool+最终 assistant；
/// MaxIterations 封顶；工具失败结果回喂且不中断。Agent/Model/LLM/MCP 全用替身，持久化用真实 PG。
/// </summary>
public sealed class ConversationToolLoopTests : IAsyncLifetime
{
    private const long AgentId = 1;
    private const long ModelId = 1;
    private const long ToolId = 10;

    private bool _available;

    public async Task InitializeAsync() => _available = await ConversationTestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static ConversationOrchestrator NewOrchestrator(
        ConversationDbContext db, IModelInvoker invoker, IMcpToolInvoker toolInvoker, int maxIterations = 5)
    {
        var contextBuilder = new ContextBuilder(
            db,
            new FakeAgentQuery().Add(FakeAgentQuery.ChatAgent(AgentId, ModelId, toolIds: [ToolId], maxIterations: maxIterations)),
            new FakeModelProviderQuery().Add(FakeModelProviderQuery.ChatModel(ModelId, supportsTools: true)),
            new NoopRetriever(),
            new CharBasedTokenEstimator(),
            new ConversationContextCache(new PassthroughCacheService()),
            new FakeMcpToolQuery(FakeMcpToolQuery.Tool(ToolId, "search")));
        return new ConversationOrchestrator(db, contextBuilder, invoker, toolInvoker, new ConversationContextCache(new PassthroughCacheService()));
    }

    private static async Task<long> SeedConversationAsync(ConversationDbContext db)
    {
        var conversation = new Domain.Conversation { AgentId = AgentId };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        return conversation.Id;
    }

    private static async Task<List<ChatEvent>> DrainAsync(ConversationOrchestrator orchestrator, ChatSession session)
    {
        var events = new List<ChatEvent>();
        await foreach (var ev in orchestrator.StreamAsync(session, CancellationToken.None))
        {
            events.Add(ev);
        }

        return events;
    }

    private static ChatResponse ToolCallTurn() => new()
    {
        FinishReason = "tool_calls",
        ToolCalls = [new ToolCall { Id = "c1", Name = "search", ArgumentsJson = """{"q":"abc"}""" }],
    };

    private static ChatResponse FinalTurn() => new() { Content = "model done", FinishReason = "stop" };

    [Fact]
    public async Task ToolLoop_ExecutesTool_FeedsResultBack_StreamsFinalAnswer()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db);

        var invoker = new ToolLoopInvoker(callIndex => callIndex == 0 ? ToolCallTurn() : FinalTurn(), "here is the answer");
        var toolInvoker = new FakeMcpToolInvoker(_ => Result<McpToolResult>.Ok(new McpToolResult { Content = "found it" }));
        var orchestrator = NewOrchestrator(db, invoker, toolInvoker);

        var prepared = await orchestrator.PrepareAsync(convId, "find my order", CancellationToken.None);
        Assert.Equal(200, prepared.Code);

        var events = await DrainAsync(orchestrator, prepared.Data!);

        // 事件流：工具发起（带入参）→ 工具结果（带返回）→ 最终答 delta → done。
        var toolCall = Assert.Single(events, e => e.Type == ChatEventType.ToolCall && e.ToolName == "search");
        Assert.Contains("abc", toolCall.ToolArguments); // 入参可展开
        var toolResult = Assert.Single(events, e => e.Type == ChatEventType.ToolResult);
        Assert.False(toolResult.ToolIsError);
        Assert.Equal("found it", toolResult.ToolResultContent); // 返回可展开
        Assert.Contains(events, e => e.Type == ChatEventType.Delta && e.Text == "here is the answer");
        Assert.Single(events, e => e.Type == ChatEventType.Done);

        // 工具被正确解析为 toolId 并执行。
        var received = Assert.Single(toolInvoker.Received);
        Assert.Equal(ToolId, received.ToolId);
        Assert.Equal("c1", received.CallId);
        Assert.Contains("abc", received.ArgumentsJson);

        // 最终流式请求带上了工具结果、且不再带 tools。
        Assert.Empty(invoker.FinalStreamRequest!.Tools);
        Assert.Contains(invoker.FinalStreamRequest.Messages, m => m.Role == MessageRoles.Tool && m.Content == "found it");

        // 落库：user, assistant(tool_calls), tool, assistant(final)。
        var messages = await db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == convId).OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(4, messages.Count);
        Assert.Equal(MessageRoles.User, messages[0].Role);
        Assert.Equal(MessageRoles.Assistant, messages[1].Role);
        Assert.Contains("search", messages[1].ToolCalls); // tool_calls 落库
        Assert.Equal(MessageRoles.Tool, messages[2].Role);
        Assert.Equal("c1", messages[2].ToolCallId);
        Assert.Equal("found it", messages[2].Content);
        Assert.Equal(MessageRoles.Assistant, messages[3].Role);
        Assert.Equal("here is the answer", messages[3].Content);
        Assert.Equal("[]", messages[3].ToolCalls); // 最终答非工具轮
    }

    [Fact]
    public async Task ToolLoop_HonorsMaxIterations()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db);

        // 模型每轮都要求调用工具；MaxIterations=2 应封顶后转最终答。
        var invoker = new ToolLoopInvoker(_ => ToolCallTurn(), "forced final");
        var toolInvoker = new FakeMcpToolInvoker(_ => Result<McpToolResult>.Ok(new McpToolResult { Content = "r" }));
        var orchestrator = NewOrchestrator(db, invoker, toolInvoker, maxIterations: 2);

        var prepared = await orchestrator.PrepareAsync(convId, "loop", CancellationToken.None);
        var events = await DrainAsync(orchestrator, prepared.Data!);

        Assert.Equal(2, invoker.ChatRequests.Count);   // 探测恰好 MaxIterations 次
        Assert.NotNull(invoker.FinalStreamRequest);     // 之后强制流式最终答
        Assert.Equal(2, toolInvoker.Received.Count);    // 每轮各执行一次工具
        Assert.Contains(events, e => e.Type == ChatEventType.Done);
    }

    [Fact]
    public async Task ToolLoop_ToolFailure_FedBackAsErrorResult_StillCompletes()
    {
        if (!_available)
        {
            return;
        }

        await using var db = ConversationTestDb.NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var convId = await SeedConversationAsync(db);

        var invoker = new ToolLoopInvoker(callIndex => callIndex == 0 ? ToolCallTurn() : FinalTurn(), "recovered");
        var toolInvoker = new FakeMcpToolInvoker(_ => Result<McpToolResult>.Fail((int)5003, "服务器不可达"));
        var orchestrator = NewOrchestrator(db, invoker, toolInvoker);

        var prepared = await orchestrator.PrepareAsync(convId, "find", CancellationToken.None);
        var events = await DrainAsync(orchestrator, prepared.Data!);

        var toolResult = Assert.Single(events, e => e.Type == ChatEventType.ToolResult);
        Assert.True(toolResult.ToolIsError); // 失败标记
        Assert.Contains(events, e => e.Type == ChatEventType.Done); // 但循环不中断，仍收尾

        // 工具结果消息回喂了失败信息。
        var toolMessage = await db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == convId && m.Role == MessageRoles.Tool).SingleAsync();
        Assert.Contains("不可达", toolMessage.Content);
    }

    /// <summary>脚本化 IModelInvoker：ChatAsync 按调用序号返回预置响应，ChatStreamAsync 流式吐最终答。</summary>
    private sealed class ToolLoopInvoker : IModelInvoker
    {
        private readonly Func<int, ChatResponse> _chatResponder;
        private readonly string _finalAnswer;

        public ToolLoopInvoker(Func<int, ChatResponse> chatResponder, string finalAnswer)
        {
            _chatResponder = chatResponder;
            _finalAnswer = finalAnswer;
        }

        public List<ChatRequest> ChatRequests { get; } = [];

        public ChatRequest? FinalStreamRequest { get; private set; }

        public Task<Result<ChatResponse>> ChatAsync(long modelId, ChatRequest request, CancellationToken cancellationToken)
        {
            var response = _chatResponder(ChatRequests.Count);
            ChatRequests.Add(request);
            return Task.FromResult(Result<ChatResponse>.Ok(response));
        }

        public Task<Result<IAsyncEnumerable<ChatStreamChunk>>> ChatStreamAsync(long modelId, ChatRequest request, CancellationToken cancellationToken)
        {
            FinalStreamRequest = request;
            return Task.FromResult(Result<IAsyncEnumerable<ChatStreamChunk>>.Ok(Stream()));
        }

        public Task<Result<EmbeddingResponse>> EmbedAsync(long modelId, EmbeddingRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        private async IAsyncEnumerable<ChatStreamChunk> Stream()
        {
            yield return new ChatStreamChunk { Delta = _finalAnswer };
            await Task.Yield();
            yield return new ChatStreamChunk { IsFinal = true, FinishReason = "stop", CompletionTokens = 5 };
        }
    }
}
