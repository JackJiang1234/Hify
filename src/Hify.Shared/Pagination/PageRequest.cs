namespace Hify.Shared.Pagination;

/// <summary>
/// OFFSET 分页请求（归一化后）。对齐接口规范：<c>page</c> 从 1 开始、<c>size</c> 默认 20 / 最大 100，
/// 且 OFFSET 最大 10000 条。请通过 <see cref="Of"/> 构造，非法入参会被裁剪到合法范围。
/// </summary>
public sealed record PageRequest
{
    /// <summary>默认每页条数。</summary>
    public const int DefaultSize = 20;

    /// <summary>每页条数上限。</summary>
    public const int MaxSize = 100;

    /// <summary>OFFSET 跳过的最大条数（超过应改用游标分页）。</summary>
    public const int MaxOffset = 10000;

    private PageRequest(int page, int size)
    {
        Page = page;
        Size = size;
    }

    /// <summary>当前页码，从 1 开始（已归一化）。</summary>
    public int Page { get; }

    /// <summary>每页条数（已归一化到 1..<see cref="MaxSize"/>）。</summary>
    public int Size { get; }

    /// <summary>需跳过的条数 = (Page-1)*Size，已封顶到 <see cref="MaxOffset"/>。</summary>
    public int Skip => Math.Min((Page - 1) * Size, MaxOffset);

    /// <summary>是否为首页（仅首页查询 total）。</summary>
    public bool IsFirstPage => Page == 1;

    /// <summary>由原始入参构造并归一化：page&lt;1 取 1；size 非法取默认、超限取上限。</summary>
    /// <param name="page">原始页码。</param>
    /// <param name="size">原始每页条数。</param>
    public static PageRequest Of(int page, int size)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedSize = size < 1 ? DefaultSize : Math.Min(size, MaxSize);
        return new PageRequest(normalizedPage, normalizedSize);
    }
}
