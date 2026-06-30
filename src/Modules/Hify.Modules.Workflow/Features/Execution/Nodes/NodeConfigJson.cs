using System.Text.Json;

namespace Hify.Modules.Workflow.Features.Execution.Nodes;

/// <summary>节点 config（原始 JSON）反序列化助手。config 缺省/为空时返回类型默认实例。</summary>
internal static class NodeConfigJson
{
    /// <summary>反序列化选项：大小写不敏感（前端 camelCase）。</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>把节点 <paramref name="config"/> 解析为强类型配置；缺省返回 <c>new T()</c>。</summary>
    /// <typeparam name="T">配置 record（需无参可构造）。</typeparam>
    /// <param name="config">节点的原始 config JSON。</param>
    public static T Read<T>(JsonElement config)
        where T : new()
    {
        if (config.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return new T();
        }

        return config.Deserialize<T>(Options) ?? new T();
    }
}

/// <summary>各节点输出字段名常量（变量引用 <c>{{nodeId.字段}}</c> 据此）。</summary>
internal static class NodeOutputField
{
    /// <summary>llm 节点输出。</summary>
    public const string Text = "text";

    /// <summary>tool 节点输出。</summary>
    public const string Result = "result";

    /// <summary>end 节点汇总输出。</summary>
    public const string Output = "output";
}
