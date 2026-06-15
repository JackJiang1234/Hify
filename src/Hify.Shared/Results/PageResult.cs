namespace Hify.Shared.Results;

/// <summary>
/// 分页响应。继承 <see cref="Result{T}"/>，<c>data</c> 即当前页列表，附加分页元信息。
/// </summary>
/// <typeparam name="T">列表元素类型。</typeparam>
public sealed record PageResult<T> : Result<IReadOnlyList<T>>
{
    /// <summary>总条数（仅首页查询，翻页不重复查）。</summary>
    public long Total { get; init; }

    /// <summary>当前页码，从 1 开始。</summary>
    public int Page { get; init; }

    /// <summary>每页条数。</summary>
    public int Size { get; init; }

    /// <summary>构造成功的分页响应（code=200）。</summary>
    /// <param name="list">当前页列表；为 <c>null</c> 时归一化为空列表（列表字段不返回 null）。</param>
    /// <param name="total">总条数。</param>
    /// <param name="page">当前页码，从 1 开始。</param>
    /// <param name="size">每页条数。</param>
    /// <param name="message">提示信息，默认 <c>"success"</c>。</param>
    public static PageResult<T> Ok(
        IReadOnlyList<T>? list,
        long total,
        int page,
        int size,
        string message = "success") => new()
    {
        Code = 200,
        Message = message,
        Data = list ?? [],
        Total = total,
        Page = page,
        Size = size,
    };
}
