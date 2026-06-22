using Hify.Modules.Conversation.Features.Chat;
using Hify.Modules.Conversation.Features.Conversations;
using Hify.Shared.Results;
using Hify.Shared.Streaming;

using Microsoft.AspNetCore.Mvc;

namespace Hify.Modules.Conversation.Endpoints;

/// <summary>
/// 发消息 + SSE 流式回复接口。准备阶段失败（会话/Agent/模型/上游初始错误）在发头之前以标准
/// <see cref="Result{T}"/> 信封返回；进入流式后逐帧推送 delta/done，中途失败推 error 帧（见设计 §6）。
/// </summary>
[ApiController]
[Route("api/v1/conversations")]
internal sealed class ChatController : ControllerBase
{
    private readonly ConversationOrchestrator _orchestrator;

    public ChatController(ConversationOrchestrator orchestrator)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        _orchestrator = orchestrator;
    }

    /// <summary>发送一条用户消息并以 SSE 流式返回助手回复。</summary>
    [HttpPost("{id:long}/messages")]
    public async Task<IActionResult> Send(long id, [FromBody] SendMessageRequest request)
    {
        var cancellationToken = HttpContext.RequestAborted;

        var prepared = await _orchestrator.PrepareAsync(id, request.Content, cancellationToken);
        if (prepared.Code != 200 || prepared.Data is null)
        {
            // 头未发出：返回标准错误信封（HTTP 200，body.code=4xxx），与其它接口一致。
            return Ok(Result<object?>.Fail(prepared.Code, prepared.Message));
        }

        // 进入流式：关缓冲，逐帧刷新（Nginx 反代另需 proxy_buffering off）。
        Response.ContentType = SseEventWriter.ContentType;
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var writer = new SseEventWriter(Response.Body);
        await foreach (var chatEvent in _orchestrator.StreamAsync(prepared.Data, cancellationToken))
        {
            await writer.WriteEventAsync(ChatEventSerializer.Serialize(chatEvent), cancellationToken);
        }

        return new EmptyResult();
    }
}
