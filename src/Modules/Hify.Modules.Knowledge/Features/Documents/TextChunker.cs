namespace Hify.Modules.Knowledge.Features.Documents;

/// <summary>
/// 固定长度分块器（按字符，带重叠）。一期分块策略：滑动窗口，窗宽 <c>chunkSize</c>、步长
/// <c>chunkSize - chunkOverlap</c>；末块到达文本结尾即止，不产生多余的纯重叠尾块。
/// 纯函数，无 I/O；调用方保证 <c>chunkSize &gt; 0</c> 且 <c>0 &lt;= chunkOverlap &lt; chunkSize</c>（建库时已校验）。
/// </summary>
internal static class TextChunker
{
    public static IReadOnlyList<string> Chunk(string text, int chunkSize, int chunkOverlap)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return [];
        }

        if (text.Length <= chunkSize)
        {
            return [text];
        }

        var step = chunkSize - chunkOverlap;
        var chunks = new List<string>();
        for (var start = 0; start < text.Length; start += step)
        {
            var length = Math.Min(chunkSize, text.Length - start);
            chunks.Add(text.Substring(start, length));

            // 本块已覆盖到结尾，停止——否则会再切出一块纯重叠的尾巴。
            if (start + chunkSize >= text.Length)
            {
                break;
            }
        }

        return chunks;
    }
}
