using Hify.Contracts.Agent;
using Hify.Modules.Conversation.Persistence;
using Hify.Shared.Pagination;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Conversation.Features.Conversations;

/// <summary>
/// 会话 CRUD 应用服务（不含发消息——流式发消息由 ConversationOrchestrator + ChatController 负责）。
/// 可预期失败返回 <see cref="Result{T}"/>（4xxx），不抛异常。
/// </summary>
internal sealed class ConversationService
{
    private readonly ConversationDbContext _db;
    private readonly IAgentQuery _agents;

    public ConversationService(ConversationDbContext db, IAgentQuery agents)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(agents);
        _db = db;
        _agents = agents;
    }

    public async Task<Result<ConversationDto>> CreateAsync(CreateConversationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Agent 必须存在且启用，才能开会话。
        var agent = await _agents.GetAgentAsync(request.AgentId, cancellationToken);
        if (agent.Code != 200 || agent.Data is null || !agent.Data.Enabled)
        {
            return Result<ConversationDto>.Fail((int)ChatErrorCode.AgentUnavailable, "Agent 不存在或已停用。");
        }

        var conversation = new Domain.Conversation { AgentId = request.AgentId, Title = string.Empty };
        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<ConversationDto>.Ok(ConversationMapping.ToDto(conversation));
    }

    public async Task<PageResult<ConversationDto>> ListAsync(int page, int size, CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Of(page, size);
        var query = _db.Conversations.AsNoTracking();

        var items = await query.ApplyPage(pageRequest)
            .Select(c => ConversationMapping.ToDto(c))
            .ToListAsync(cancellationToken);
        var total = pageRequest.IsFirstPage ? await query.CountAsync(cancellationToken) : 0;

        return PageResult<ConversationDto>.Ok(items, total, pageRequest.Page, pageRequest.Size);
    }

    public async Task<PageResult<MessageDto>> GetHistoryAsync(long conversationId, int page, int size, CancellationToken cancellationToken)
    {
        if (!await _db.Conversations.AnyAsync(c => c.Id == conversationId, cancellationToken))
        {
            return new PageResult<MessageDto>
            {
                Code = (int)ChatErrorCode.ConversationNotFound,
                Message = "会话不存在。",
                Data = [],
            };
        }

        var pageRequest = PageRequest.Of(page, size);
        var query = _db.Messages.AsNoTracking().Where(m => m.ConversationId == conversationId);

        var items = await query.ApplyPage(pageRequest)
            .Select(m => ConversationMapping.ToDto(m))
            .ToListAsync(cancellationToken);
        var total = pageRequest.IsFirstPage ? await query.CountAsync(cancellationToken) : 0;

        return PageResult<MessageDto>.Ok(items, total, pageRequest.Page, pageRequest.Size);
    }

    public async Task<Result<bool>> DeleteAsync(long conversationId, CancellationToken cancellationToken)
    {
        var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return Result<bool>.Fail((int)ChatErrorCode.ConversationNotFound, "会话不存在。");
        }

        // 级联软删：会话 + 其消息（SaveChanges 由 DbContext 转为软删）。
        var messages = await _db.Messages.Where(m => m.ConversationId == conversationId).ToListAsync(cancellationToken);
        _db.Messages.RemoveRange(messages);
        _db.Conversations.Remove(conversation);

        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }
}
