namespace Hify.Modules.Conversation;

/// <summary>
/// Conversation 模块错误码（4xxx 段）。枚举值即对外返回的四位业务码。
/// 格式/范围校验失败由全局校验过滤器统一返回通用码 1001，不在此枚举内。
/// 流式接口在首字之前的失败以这些码经 <c>Result</c> 返回；流中途失败经 SSE error 帧携带。
/// </summary>
internal enum ChatErrorCode
{
    /// <summary>会话不存在。</summary>
    ConversationNotFound = 4001,

    /// <summary>引用的 Agent 不存在或已停用。</summary>
    AgentUnavailable = 4002,

    /// <summary>Agent 绑定的模型不存在或已停用。</summary>
    ModelUnavailable = 4003,

    /// <summary>用户输入为空或超长。</summary>
    InvalidInput = 4004,

    /// <summary>上游 LLM 调用失败。</summary>
    UpstreamLlmFailed = 4005,

    /// <summary>上下文超出模型窗口且无法裁剪。</summary>
    ContextOverflow = 4007,
}
