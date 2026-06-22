using FluentValidation;

namespace Hify.Modules.Conversation.Features.Conversations;

/// <summary>创建会话请求。Agent 的存在性在服务层经 IAgentQuery 校验。</summary>
internal sealed record CreateConversationRequest
{
    /// <summary>绑定的 Agent Id。</summary>
    public long AgentId { get; init; }
}

/// <summary>发送消息请求（聊天接口入参）。</summary>
internal sealed record SendMessageRequest
{
    /// <summary>用户输入内容。</summary>
    public string Content { get; init; } = string.Empty;
}

/// <summary>会话请求的共用上下界。</summary>
internal static class ConversationValidation
{
    /// <summary>单条用户输入最大长度（字符）。超长由全局校验过滤器返回通用码 1001。</summary>
    public const int MaxContentLength = 32000;
}

/// <summary>创建会话请求校验。</summary>
internal sealed class CreateConversationRequestValidator : AbstractValidator<CreateConversationRequest>
{
    public CreateConversationRequestValidator()
    {
        RuleFor(request => request.AgentId).GreaterThan(0).WithMessage("agentId 非法");
    }
}

/// <summary>发送消息请求校验。</summary>
internal sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(request => request.Content)
            .NotEmpty().WithMessage("content 不能为空")
            .MaximumLength(ConversationValidation.MaxContentLength).WithMessage($"content 不超过 {ConversationValidation.MaxContentLength} 字符");
    }
}
