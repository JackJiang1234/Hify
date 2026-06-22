using Hify.Modules.Conversation.Features.Conversations;
using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;

namespace Hify.Modules.Conversation.Endpoints;

/// <summary>会话管理接口（创建/列表/历史/删除）。统一返回 <see cref="Result{T}"/>；入参校验由全局过滤器执行。</summary>
[ApiController]
[Route("api/v1/conversations")]
internal sealed class ConversationsController : ControllerBase
{
    private readonly ConversationService _service;

    public ConversationsController(ConversationService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>新建会话（绑定 Agent）。</summary>
    [HttpPost]
    public Task<Result<ConversationDto>> Create([FromBody] CreateConversationRequest request) =>
        _service.CreateAsync(request, HttpContext.RequestAborted);

    /// <summary>分页列出会话（按最近活跃倒序）。</summary>
    [HttpGet]
    public Task<PageResult<ConversationDto>> List([FromQuery] int page = 1, [FromQuery] int size = 20) =>
        _service.ListAsync(page, size, HttpContext.RequestAborted);

    /// <summary>分页查询会话历史消息（按 id 倒序，最新在前）。</summary>
    [HttpGet("{id:long}/messages")]
    public Task<PageResult<MessageDto>> History(long id, [FromQuery] int page = 1, [FromQuery] int size = 20) =>
        _service.GetHistoryAsync(id, page, size, HttpContext.RequestAborted);

    /// <summary>删除会话（级联软删消息）。</summary>
    [HttpDelete("{id:long}")]
    public Task<Result<bool>> Delete(long id) =>
        _service.DeleteAsync(id, HttpContext.RequestAborted);
}
