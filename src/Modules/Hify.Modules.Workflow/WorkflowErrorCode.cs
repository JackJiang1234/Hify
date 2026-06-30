namespace Hify.Modules.Workflow;

/// <summary>
/// Workflow 模块错误码（6xxx 段）。枚举值即对外返回的四位业务码。
/// 格式/范围校验失败由全局校验过滤器统一返回通用码 1001，不在此枚举内。
/// </summary>
internal enum WorkflowErrorCode
{
    /// <summary>工作流不存在。</summary>
    WorkflowNotFound = 6001,

    /// <summary>定义非法（图校验未过：缺 start/end、多入多出、有环、变量引用不可解析等）。</summary>
    InvalidDefinition = 6002,

    /// <summary>试运行输入缺失 / 不满足 start 声明的 required。</summary>
    InvalidRunInput = 6003,

    /// <summary>节点执行失败（LLM / 工具上游错误）。</summary>
    NodeExecutionFailed = 6004,

    /// <summary>执行超出最大步数（疑似环 / 失控）。</summary>
    MaxStepsExceeded = 6005,

    /// <summary>执行超时（同步总超时）。</summary>
    ExecutionTimeout = 6006,

    /// <summary>引用的模型 / Agent / MCP 工具不存在或已停用。</summary>
    ReferenceUnavailable = 6007,

    /// <summary>工作流名称重复。</summary>
    NameConflict = 6008,

    /// <summary>运行记录不存在。</summary>
    RunNotFound = 6009,
}
