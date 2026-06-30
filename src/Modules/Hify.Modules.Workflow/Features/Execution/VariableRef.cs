using System.Text.RegularExpressions;

namespace Hify.Modules.Workflow.Features.Execution;

/// <summary>
/// 变量引用语法 <c>{{nodeId.field.path}}</c> 的解析助手（校验与执行共用）。
/// 第一段为节点 Id，其后为该节点输出对象内的字段路径（点分）。
/// </summary>
internal static partial class VariableRef
{
    /// <summary>一处变量引用：节点 Id + 字段路径（点分，可多级）。</summary>
    internal readonly record struct Reference(string NodeId, string Path);

    /// <summary>匹配 <c>{{ nodeId.field.path }}</c>；group1=nodeId，group2=字段路径。</summary>
    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_]+)\.([A-Za-z0-9_.]+)\s*\}\}", RegexOptions.CultureInvariant)]
    public static partial Regex Pattern();

    /// <summary>抽取文本中全部变量引用（按出现顺序，含重复）。无引用返回空列表。</summary>
    /// <param name="text">待扫描文本（通常为节点 config 的原始 JSON）。</param>
    public static IReadOnlyList<Reference> Extract(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var references = new List<Reference>();
        foreach (Match match in Pattern().Matches(text))
        {
            references.Add(new Reference(match.Groups[1].Value, match.Groups[2].Value));
        }

        return references;
    }
}
