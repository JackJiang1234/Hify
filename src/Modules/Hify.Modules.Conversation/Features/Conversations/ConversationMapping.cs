using Hify.Modules.Conversation.Domain;

namespace Hify.Modules.Conversation.Features.Conversations;

/// <summary>实体 → 视图映射。</summary>
internal static class ConversationMapping
{
    public static ConversationDto ToDto(Domain.Conversation conversation) => new()
    {
        Id = conversation.Id,
        AgentId = conversation.AgentId,
        Title = conversation.Title,
        CreatedAt = conversation.CreatedAt,
        UpdatedAt = conversation.UpdatedAt,
    };

    public static MessageDto ToDto(Message message) => new()
    {
        Id = message.Id,
        ConversationId = message.ConversationId,
        Role = message.Role,
        Content = message.Content,
        FinishReason = message.FinishReason,
        Status = message.Status,
        PromptTokens = message.PromptTokens,
        CompletionTokens = message.CompletionTokens,
        CreatedAt = message.CreatedAt,
    };
}
