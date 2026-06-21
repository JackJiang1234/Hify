namespace Hify.Modules.Agent;

/// <summary>
/// Agent 模块错误码（3xxx 段）。枚举值即对外返回的四位业务码。
/// 格式/范围校验失败由全局校验过滤器统一返回通用码 1001，不在此枚举内。
/// </summary>
internal enum AgentErrorCode
{
    /// <summary>Agent 不存在。</summary>
    AgentNotFound = 3001,

    /// <summary>Agent 名称冲突。</summary>
    AgentNameConflict = 3002,

    /// <summary>引用的模型非法（不存在 / 非 chat 类型 / 已停用 / MaxTokens 超模型上限）。</summary>
    AgentModelInvalid = 3003,

    /// <summary>引用的工具非法（不存在 / 已停用）。</summary>
    AgentToolInvalid = 3004,

    /// <summary>引用的知识库非法（不存在）。</summary>
    AgentKnowledgeInvalid = 3005,

    /// <summary>绑定了工具但所选模型不支持工具调用。</summary>
    ModelToolUnsupported = 3006,
}
