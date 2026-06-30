namespace Hify.Modules.Workflow.Domain;

/// <summary>工作流节点类型常量（对齐 definition.nodes[].type 取值）。一期五种。</summary>
internal static class WorkflowNodeType
{
    /// <summary>开始：声明工作流输入。唯一，无入边。</summary>
    public const string Start = "start";

    /// <summary>大模型：内联 modelId + prompt 调用 LLM，输出 <c>text</c>。</summary>
    public const string Llm = "llm";

    /// <summary>工具：调用 MCP 工具，输出 <c>result</c>。</summary>
    public const string Tool = "tool";

    /// <summary>条件分支：按单比较选择出边 handle。</summary>
    public const string Condition = "condition";

    /// <summary>结束：汇总输出为 run.output。无出边。</summary>
    public const string End = "end";

    /// <summary>全部合法类型。</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Start, Llm, Tool, Condition, End,
    };
}
