using System.Globalization;
using System.Text.Json;

using NodeOutputs = System.Collections.Generic.IReadOnlyDictionary<
    string,
    System.Collections.Generic.IReadOnlyDictionary<string, object?>>;

namespace Hify.Modules.Workflow.Features.Execution;

/// <summary>
/// 变量解析器：把模板里的 <c>{{nodeId.field.path}}</c> 用执行期各节点输出替换为实际值。
/// 纯函数、无 I/O。节点输出按 nodeId 索引，再按点分路径在字段对象内逐级下钻
/// （支持嵌套 <see cref="IReadOnlyDictionary{TKey,TValue}"/> 与 <see cref="JsonElement"/>）。
/// 引用在校验期（<see cref="Definitions.DefinitionValidator"/>）已保证 nodeId 在前驱链上，
/// 运行期仅字段可能缺失——缺失按空串处理，不抛异常。
/// </summary>
internal sealed class VariableResolver
{
    /// <summary>把模板中全部变量引用替换为字符串值；无引用则原样返回。</summary>
    /// <param name="template">含 <c>{{...}}</c> 的模板（如 prompt、output 表达式）。</param>
    /// <param name="outputs">各节点输出（nodeId -&gt; 字段 -&gt; 值）。</param>
    public string ResolveString(string template, NodeOutputs outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);

        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        return VariableRef.Pattern().Replace(
            template,
            match =>
            {
                var reference = new VariableRef.Reference(match.Groups[1].Value, match.Groups[2].Value);
                return TryResolveValue(reference, outputs, out var value) ? Stringify(value) : string.Empty;
            });
    }

    /// <summary>解析单个引用为其原始值（供数值比较等需要保留类型的场景）。缺失返回 false。</summary>
    /// <param name="reference">变量引用。</param>
    /// <param name="outputs">各节点输出。</param>
    /// <param name="value">解析到的原始值（找到时）。</param>
    public bool TryResolveValue(VariableRef.Reference reference, NodeOutputs outputs, out object? value)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        value = null;

        if (!outputs.TryGetValue(reference.NodeId, out var nodeOutput) || nodeOutput is null)
        {
            return false;
        }

        var segments = reference.Path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        if (!nodeOutput.TryGetValue(segments[0], out var current))
        {
            return false;
        }

        for (var i = 1; i < segments.Length; i++)
        {
            if (!TryDescend(current, segments[i], out current))
            {
                return false;
            }
        }

        value = current;
        return true;
    }

    // 在一个对象值里按字段名下钻一层（支持字典与 JSON 对象）。
    private static bool TryDescend(object? container, string field, out object? next)
    {
        switch (container)
        {
            case IReadOnlyDictionary<string, object?> dict:
                return dict.TryGetValue(field, out next);

            case JsonElement element when element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(field, out var property):
                next = property;
                return true;

            default:
                next = null;
                return false;
        }
    }

    // 把任意值转成字符串以插入模板。字符串/数值/布尔直出；JSON 标量取标量、对象/数组取原文。
    private static string Stringify(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            bool b => b ? "true" : "false",
            JsonElement element => StringifyJson(element),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string StringifyJson(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.GetRawText(),
        };
    }
}
