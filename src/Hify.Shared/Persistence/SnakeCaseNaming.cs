using System.Text;

namespace Hify.Shared.Persistence;

/// <summary>
/// 标识符转 snake_case 工具。用于将 C# 的 PascalCase 表名/列名映射为数据库 snake_case 命名，
/// 对齐数据库规范（如 <c>CreatedAt</c> → <c>created_at</c>、<c>ConversationId</c> → <c>conversation_id</c>）。
/// </summary>
public static class SnakeCaseNaming
{
    /// <summary>
    /// 将标识符转为小写 snake_case：在小写/数字与大写之间、以及连续大写到「大写+小写」边界处插入下划线。
    /// 已含的下划线保留，不产生连续下划线。空白输入原样返回。
    /// </summary>
    /// <param name="name">原标识符（通常为 PascalCase）。</param>
    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (current == '_')
            {
                AppendUnderscore(builder);
                continue;
            }

            if (char.IsUpper(current) && i > 0 && NeedsBoundary(name, i))
            {
                AppendUnderscore(builder);
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }

    // 大写字母处需要分词，当：前一个字符是小写/数字（aB → a_b），
    // 或处于连续大写的词尾即后一个字符是小写（HTTPServer 中 PS → p_s）。
    private static bool NeedsBoundary(string name, int index)
    {
        var previous = name[index - 1];
        if (char.IsLower(previous) || char.IsDigit(previous))
        {
            return true;
        }

        var hasNext = index + 1 < name.Length;
        return char.IsUpper(previous) && hasNext && char.IsLower(name[index + 1]);
    }

    private static void AppendUnderscore(StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != '_')
        {
            builder.Append('_');
        }
    }
}
