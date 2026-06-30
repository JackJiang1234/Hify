namespace Hify.Contracts.ModelProvider;

/// <summary>
/// 供模型选择调用的工具定义（供应商无关）。由对话引擎从 MCP 工具元数据映射而来，随 <see cref="ChatRequest.Tools"/> 下发。
/// </summary>
public record ToolDefinition
{
    /// <summary>工具名（模型按此名发起调用）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>工具用途描述（供模型判断何时调用）。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>入参 JSON Schema（原样 JSON 字符串）。</summary>
    public string ParametersJson { get; init; } = "{}";
}
