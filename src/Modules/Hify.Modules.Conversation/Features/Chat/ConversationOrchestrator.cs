using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Hify.Contracts.Mcp;
using Hify.Contracts.ModelProvider;
using Hify.Modules.Conversation.Domain;
using Hify.Modules.Conversation.Features.Context;
using Hify.Modules.Conversation.Persistence;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Conversation.Features.Chat;

/// <summary>
/// 已就绪的流式对话会话。无工具路径预开上游流（<see cref="Stream"/>）；工具路径携 <see cref="Prepared"/>，
/// 循环在 <see cref="ConversationOrchestrator.StreamAsync"/> 内驱动。
/// </summary>
internal sealed class ChatSession
{
    public required long ConversationId { get; init; }

    public required long ModelId { get; init; }

    /// <summary>无工具路径：预开的上游流；工具路径为 null。</summary>
    public IAsyncEnumerable<ChatStreamChunk>? Stream { get; init; }

    /// <summary>工具路径：装配好的请求（含 Tools 与工具名映射）；无工具路径为 null。</summary>
    public PreparedChat? Prepared { get; init; }
}

/// <summary>
/// 对话引擎主流程。两阶段对齐 SSE「首字」语义：<see cref="PrepareAsync"/> 在发响应头前完成校验/装配/落库用户消息，
/// 失败以 <see cref="Result{T}"/>（4xxx）返回；<see cref="StreamAsync"/> 逐片产出 <see cref="ChatEvent"/>。
/// 无工具时单流透传；有工具时跑「非流式迭代探测工具 + 最终答流式」循环（迭代用 ChatAsync，最终答用 ChatStreamAsync）。
/// </summary>
internal sealed class ConversationOrchestrator
{
    private const int TitleMaxLength = 50;
    private const int ErrorMessageMaxLength = 512;

    private readonly ConversationDbContext _db;
    private readonly ContextBuilder _contextBuilder;
    private readonly IModelInvoker _invoker;
    private readonly IMcpToolInvoker _toolInvoker;
    private readonly ConversationContextCache _cache;

    public ConversationOrchestrator(
        ConversationDbContext db,
        ContextBuilder contextBuilder,
        IModelInvoker invoker,
        IMcpToolInvoker toolInvoker,
        ConversationContextCache cache)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contextBuilder);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(toolInvoker);
        ArgumentNullException.ThrowIfNull(cache);
        _db = db;
        _contextBuilder = contextBuilder;
        _invoker = invoker;
        _toolInvoker = toolInvoker;
        _cache = cache;
    }

    /// <summary>
    /// 发响应头前的准备：校验会话、装配上下文（含工具）、落库用户消息、回填标题。
    /// 无工具时预开上游流（初始连通/认证失败仍可作 Result 返回）；有工具时循环延后到 <see cref="StreamAsync"/>。
    /// </summary>
    public async Task<Result<ChatSession>> PrepareAsync(long conversationId, string userInput, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userInput);

        var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return Result<ChatSession>.Fail((int)ChatErrorCode.ConversationNotFound, "会话不存在。");
        }

        var prepared = await _contextBuilder.BuildAsync(conversationId, conversation.AgentId, userInput, cancellationToken);
        if (prepared.Code != 200 || prepared.Data is null)
        {
            return Result<ChatSession>.Fail(prepared.Code, prepared.Message);
        }

        // 落库用户消息；标题为空则用首条用户输入截断回填。
        _db.Messages.Add(new Message
        {
            ConversationId = conversationId,
            Role = MessageRoles.User,
            Content = userInput,
            Status = MessageStatus.Completed,
        });

        if (string.IsNullOrEmpty(conversation.Title))
        {
            conversation.Title = Truncate(userInput, TitleMaxLength);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateAsync(conversationId, cancellationToken);

        // 有工具：循环（首个 ChatAsync）在 StreamAsync 内驱动，初始上游失败将作为 error 事件。
        if (prepared.Data.Request.Tools.Count > 0)
        {
            return Result<ChatSession>.Ok(new ChatSession
            {
                ConversationId = conversationId,
                ModelId = prepared.Data.ModelId,
                Prepared = prepared.Data,
            });
        }

        // 无工具：预开上游流，初始失败（连通/认证/限流）此刻仍可作 Result 返回（头未发出）。
        var streamResult = await _invoker.ChatStreamAsync(prepared.Data.ModelId, prepared.Data.Request, cancellationToken);
        if (streamResult.Code != 200 || streamResult.Data is null)
        {
            return Result<ChatSession>.Fail((int)ChatErrorCode.UpstreamLlmFailed, "上游模型调用失败。");
        }

        return Result<ChatSession>.Ok(new ChatSession
        {
            ConversationId = conversationId,
            ModelId = prepared.Data.ModelId,
            Stream = streamResult.Data,
        });
    }

    /// <summary>逐片产出对话事件：无工具走单流；有工具走工具循环 + 最终答流式。</summary>
    public IAsyncEnumerable<ChatEvent> StreamAsync(ChatSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.Stream is not null
            ? StreamAndPersistAsync(session.Stream, session.ConversationId, session.ModelId, cancellationToken)
            : RunToolLoopAsync(session.Prepared!, session.ConversationId, cancellationToken);
    }

    /// <summary>
    /// 工具循环：非流式 ChatAsync 探测——有 tool_calls 则执行并回喂、继续；无 tool_calls 或到上限则用 ChatStreamAsync 流式产出最终答。
    /// </summary>
    private async IAsyncEnumerable<ChatEvent> RunToolLoopAsync(
        PreparedChat prepared,
        long conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>(prepared.Request.Messages);
        var maxIterations = Math.Max(1, prepared.MaxIterations);

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var probeRequest = prepared.Request with { Messages = messages };

            Result<ChatResponse>? probe = null;
            var cancelled = false;
            try
            {
                probe = await _invoker.ChatAsync(prepared.ModelId, probeRequest, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            if (cancelled)
            {
                yield break; // 客户端已离开，不再产出
            }

            if (probe!.Code != 200 || probe.Data is null)
            {
                await PersistAssistantAsync(conversationId, prepared.ModelId, string.Empty, MessageStatus.Failed, "error", 0, 0, probe.Message);
                yield return ChatEvent.Error((int)ChatErrorCode.UpstreamLlmFailed, "工具循环中上游模型出错。");
                yield break;
            }

            if (probe.Data.ToolCalls.Count == 0)
            {
                break; // 模型不再调用工具 → 去流式产出最终答
            }

            // 发起调用事件（执行前），便于前端展示「正在调用工具」并可展开看入参。
            foreach (var call in probe.Data.ToolCalls)
            {
                yield return ChatEvent.ToolCallStarted(call.Id, call.Name, call.ArgumentsJson);
            }

            // assistant(tool_calls) 必须先于 tool 结果入消息序列（供应商要求）。
            await PersistAssistantToolCallsAsync(conversationId, prepared.ModelId, probe.Data);
            messages.Add(new ChatMessage
            {
                Role = MessageRoles.Assistant,
                Content = probe.Data.Content,
                ToolCalls = probe.Data.ToolCalls,
            });

            var resultsByCallId = await ExecuteToolsAsync(probe.Data.ToolCalls, prepared.ToolIdsByName, cancellationToken);

            foreach (var call in probe.Data.ToolCalls)
            {
                var (content, isError) = resultsByCallId[call.Id];
                await PersistToolResultAsync(conversationId, call.Id, content);
                messages.Add(new ChatMessage { Role = MessageRoles.Tool, Content = content, ToolCallId = call.Id });
                yield return ChatEvent.ToolCallResult(call.Id, call.Name, isError, content);
            }
        }

        // 最终答：去掉 tools，流式产出（兑现「只流最终答」）。
        var finalRequest = prepared.Request with { Messages = messages, Tools = [] };

        Result<IAsyncEnumerable<ChatStreamChunk>>? finalStream = null;
        var streamCancelled = false;
        try
        {
            finalStream = await _invoker.ChatStreamAsync(prepared.ModelId, finalRequest, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            streamCancelled = true;
        }

        if (streamCancelled)
        {
            yield break;
        }

        if (finalStream!.Code != 200 || finalStream.Data is null)
        {
            await PersistAssistantAsync(conversationId, prepared.ModelId, string.Empty, MessageStatus.Failed, "error", 0, 0, finalStream.Message);
            yield return ChatEvent.Error((int)ChatErrorCode.UpstreamLlmFailed, "生成最终回复时上游模型出错。");
            yield break;
        }

        await foreach (var chatEvent in StreamAndPersistAsync(finalStream.Data, conversationId, prepared.ModelId, cancellationToken))
        {
            yield return chatEvent;
        }
    }

    /// <summary>映射并并发执行工具调用，返回按 callId 索引的（文本, 是否报错）。未知工具名以错误结果回喂。</summary>
    private async Task<IReadOnlyDictionary<string, (string Content, bool IsError)>> ExecuteToolsAsync(
        IReadOnlyList<ToolCall> toolCalls,
        IReadOnlyDictionary<string, long> toolIdsByName,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, (string, bool)>(StringComparer.Ordinal);

        var mcpCalls = new List<McpToolCall>();
        foreach (var call in toolCalls)
        {
            if (toolIdsByName.TryGetValue(call.Name, out var toolId))
            {
                mcpCalls.Add(new McpToolCall { CallId = call.Id, ToolId = toolId, ArgumentsJson = call.ArgumentsJson });
            }
            else
            {
                results[call.Id] = ($"未知工具：{call.Name}", true);
            }
        }

        if (mcpCalls.Count > 0)
        {
            var invocations = await _toolInvoker.InvokeManyAsync(mcpCalls, cancellationToken);
            foreach (var invocation in invocations)
            {
                var content = invocation.Result.Code == 200
                    ? invocation.Result.Data!.Content
                    : invocation.Result.Message;
                var isError = invocation.Result.Code != 200 || (invocation.Result.Data?.IsError ?? false);
                results[invocation.CallId] = (content, isError);
            }
        }

        return results;
    }

    /// <summary>逐片消费上游流：产出 delta；结束落库 assistant 并产出 done；中途异常落 failed 并产 error；取消落 cancelled。</summary>
    private async IAsyncEnumerable<ChatEvent> StreamAndPersistAsync(
        IAsyncEnumerable<ChatStreamChunk> stream,
        long conversationId,
        long modelId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var content = new StringBuilder();
        var finishReason = string.Empty;
        long promptTokens = 0;
        long completionTokens = 0;
        var cancelled = false;
        string? errorMessage = null;

        await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            ChatStreamChunk chunk;
            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    break;
                }

                chunk = enumerator.Current;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                break;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                break;
            }

            if (chunk.IsFinal)
            {
                finishReason = chunk.FinishReason;
                promptTokens = chunk.PromptTokens;
                completionTokens = chunk.CompletionTokens;
                continue;
            }

            if (!string.IsNullOrEmpty(chunk.Delta))
            {
                content.Append(chunk.Delta);
                yield return ChatEvent.Delta(chunk.Delta);
            }
        }

        var status = cancelled
            ? MessageStatus.Cancelled
            : errorMessage is not null ? MessageStatus.Failed : MessageStatus.Completed;
        var persistedFinishReason = errorMessage is not null ? "error" : finishReason;

        var messageId = await PersistAssistantAsync(
            conversationId,
            modelId,
            content.ToString(),
            status,
            persistedFinishReason,
            promptTokens,
            completionTokens,
            errorMessage);

        if (errorMessage is not null)
        {
            yield return ChatEvent.Error((int)ChatErrorCode.UpstreamLlmFailed, "生成过程中上游模型出错。");
        }
        else if (!cancelled)
        {
            yield return ChatEvent.Done(messageId, finishReason, promptTokens, completionTokens);
        }
    }

    private async Task<long> PersistAssistantAsync(
        long conversationId,
        long modelId,
        string content,
        string status,
        string finishReason,
        long promptTokens,
        long completionTokens,
        string? errorMessage)
    {
        var message = new Message
        {
            ConversationId = conversationId,
            Role = MessageRoles.Assistant,
            Content = content,
            ModelId = modelId,
            Status = status,
            FinishReason = finishReason,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            ErrorMessage = errorMessage is null ? string.Empty : Truncate(errorMessage, ErrorMessageMaxLength),
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync(CancellationToken.None);
        await _cache.InvalidateAsync(conversationId, CancellationToken.None);
        return message.Id;
    }

    private async Task PersistAssistantToolCallsAsync(long conversationId, long modelId, ChatResponse response)
    {
        _db.Messages.Add(new Message
        {
            ConversationId = conversationId,
            Role = MessageRoles.Assistant,
            Content = response.Content,
            ModelId = modelId,
            Status = MessageStatus.Completed,
            FinishReason = "tool_calls",
            ToolCalls = SerializeToolCalls(response.ToolCalls),
            PromptTokens = response.PromptTokens,
            CompletionTokens = response.CompletionTokens,
        });
        await _db.SaveChangesAsync(CancellationToken.None);
        await _cache.InvalidateAsync(conversationId, CancellationToken.None);
    }

    private async Task PersistToolResultAsync(long conversationId, string callId, string content)
    {
        _db.Messages.Add(new Message
        {
            ConversationId = conversationId,
            Role = MessageRoles.Tool,
            Content = content,
            ToolCallId = callId,
            Status = MessageStatus.Completed,
        });
        await _db.SaveChangesAsync(CancellationToken.None);
    }

    private static string SerializeToolCalls(IReadOnlyList<ToolCall> toolCalls) =>
        JsonSerializer.Serialize(toolCalls.Select(call => new
        {
            id = call.Id,
            name = call.Name,
            arguments = call.ArgumentsJson,
        }));

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];
}
