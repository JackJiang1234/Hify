namespace Hify.Contracts.ModelProvider;

/// <summary>模型发起的一次工具调用（供应商无关）。由适配器从响应解析，对话引擎据此执行工具并回喂结果。</summary>
public record ToolCall
{
    /// <summary>调用 Id（供应商生成，用于把工具结果回指到本次调用）。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>被调用的工具名。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>调用入参（原样 JSON 字符串）。</summary>
    public string ArgumentsJson { get; init; } = "{}";
}
