using Hify.Shared.Persistence;

namespace Hify.Shared.Pagination;

/// <summary>
/// 分页查询扩展。统一两种分页方式：默认游标分页（大表、无限下拉），OFFSET 分页（带页码的后台列表）。
/// 均按主键倒序，对齐数据库规范「<c>WHERE id &lt; lastId ORDER BY id DESC LIMIT N</c>」。
/// </summary>
public static class QueryablePaginationExtensions
{
    /// <summary>
    /// 游标分页：取 id 小于 <paramref name="lastId"/> 的前 N 条，按 id 倒序。
    /// 首页传 <paramref name="lastId"/> = 0（或不大于 0）。<paramref name="size"/> 归一化到 1..<see cref="PageRequest.MaxSize"/>。
    /// </summary>
    /// <typeparam name="T">实体类型。</typeparam>
    /// <param name="source">查询源。</param>
    /// <param name="lastId">上一页最后一条的 Id；首页传 0。</param>
    /// <param name="size">本页条数。</param>
    public static IQueryable<T> ApplyCursor<T>(this IQueryable<T> source, long lastId, int size)
        where T : EntityBase
    {
        ArgumentNullException.ThrowIfNull(source);

        var take = NormalizeSize(size);
        var filtered = lastId > 0 ? source.Where(entity => entity.Id < lastId) : source;
        return filtered.OrderByDescending(entity => entity.Id).Take(take);
    }

    /// <summary>
    /// OFFSET 分页：按 <paramref name="request"/> 跳过并取一页，按 id 倒序。
    /// </summary>
    /// <typeparam name="T">实体类型。</typeparam>
    /// <param name="source">查询源。</param>
    /// <param name="request">已归一化的分页请求。</param>
    public static IQueryable<T> ApplyPage<T>(this IQueryable<T> source, PageRequest request)
        where T : EntityBase
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        return source
            .OrderByDescending(entity => entity.Id)
            .Skip(request.Skip)
            .Take(request.Size);
    }

    private static int NormalizeSize(int size)
    {
        if (size < 1)
        {
            return PageRequest.DefaultSize;
        }

        return Math.Min(size, PageRequest.MaxSize);
    }
}
