using Hify.Contracts.ModelProvider;
using Hify.Modules.Conversation.Domain;
using Hify.Modules.Conversation.Features.Context;
using Hify.Modules.Conversation.Persistence;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Conversation.Features.Chat;

/// <summary>
/// 已就绪的流式对话会话：装配完成、用户消息已落库、上游流已建立，等待逐片消费。
/// </summary>
internal sealed class ChatSession
{
    public required long ConversationId { get; init; }

    public required long ModelId { get; init; }

    public required IAsyncEnumerable<ChatStreamChunk> Stream { get; init; }
}

/// <summary>
/// 对话引擎主流程（一期纯文本，无工具循环）。分两阶段，对齐 SSE「首字」语义：
/// <see cref="PrepareAsync"/> 在发出响应头之前完成校验/装配/落库与建立上游流，失败以 <see cref="Result{T}"/>（4xxx）返回；
/// <see cref="StreamAsync"/> 逐片产出 <see cref="ChatEvent"/>，中途失败以 error 事件携带、并把 assistant 消息落为 failed/cancelled。
/// </summary>
internal sealed class ConversationOrchestrator
{
    private const int TitleMaxLength = 50;
    private const int ErrorMessageMaxLength = 512;

    private readonly ConversationDbContext _db;
    private readonly ContextBuilder _contextBuilder;
    private readonly IModelInvoker _invoker;
    private readonly ConversationContextCache _cache;

    public ConversationOrchestrator(
        ConversationDbContext db,
        ContextBuilder contextBuilder,
        IModelInvoker invoker,
        ConversationContextCache cache)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contextBuilder);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(cache);
        _db = db;
        _contextBuilder = contextBuilder;
        _invoker = invoker;
        _cache = cache;
    }

    /// <summary>
    /// 发出响应头之前的准备：校验会话、装配上下文、落库用户消息、回填标题、建立上游流。
    /// 任一步失败以 Result（4xxx）返回，控制器据此返回标准错误信封（头未发出）。
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

        // 落库用户消息；标题为空则用首条用户输入截断回填（设计 D）。
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

        // 历史已变（新增 user 消息），失效缓存，确保后续回源看到最新。
        await _cache.InvalidateAsync(conversationId, cancellationToken);

        // 建立上游流：初始失败（连通/认证/限流）此刻仍可作为 Result 返回（头未发出）。
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

    /// <summary>
    /// 逐片消费上游流：产出 delta；结束后落库 assistant 消息并产出 done；
    /// 中途异常产出 error 并落 failed；取消则落 cancelled（不再产出，客户端已离开）。
    /// </summary>
    public async IAsyncEnumerable<ChatEvent> StreamAsync(
        ChatSession session,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        var content = new System.Text.StringBuilder();
        var finishReason = string.Empty;
        long promptTokens = 0;
        long completionTokens = 0;
        var cancelled = false;
        string? errorMessage = null;

        await using var enumerator = session.Stream.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            ChatStreamChunk chunk;

            // yield 不能置于 try-catch 内：手动迭代，异常在循环外转为事件。
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

        // 取消时请求令牌已失效；落库用 None 保证记录不丢。
        var messageId = await PersistAssistantAsync(
            session.ConversationId,
            session.ModelId,
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

        // 助手消息落库后历史再次变化，失效缓存（含取消/失败场景，故用 None）。
        await _cache.InvalidateAsync(conversationId, CancellationToken.None);
        return message.Id;
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];
}
