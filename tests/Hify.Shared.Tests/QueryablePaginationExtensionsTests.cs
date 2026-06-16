using Hify.Shared.Pagination;
using Hify.Shared.Persistence;

namespace Hify.Shared.Tests;

public class QueryablePaginationExtensionsTests
{
    private sealed class Sample : EntityBase
    {
    }

    private static IQueryable<Sample> Build(params long[] ids)
    {
        return ids.Select(id => new Sample { Id = id }).AsQueryable();
    }

    [Fact]
    public void ApplyCursor_FirstPage_TakesTopByIdDescending()
    {
        var query = Build(1, 2, 3, 4, 5);

        var page = query.ApplyCursor(lastId: 0, size: 2).ToList();

        Assert.Equal(new long[] { 5, 4 }, page.Select(entity => entity.Id));
    }

    [Fact]
    public void ApplyCursor_WithLastId_TakesOnlyOlderRows()
    {
        var query = Build(1, 2, 3, 4, 5);

        var page = query.ApplyCursor(lastId: 4, size: 10).ToList();

        Assert.Equal(new long[] { 3, 2, 1 }, page.Select(entity => entity.Id));
    }

    [Theory]
    [InlineData(0, PageRequest.DefaultSize)]   // size<1 → 默认
    [InlineData(1000, PageRequest.MaxSize)]    // 超限 → 上限
    public void ApplyCursor_NormalizesSize(int size, int expectedTake)
    {
        var ids = Enumerable.Range(1, 500).Select(value => (long)value).ToArray();

        var page = Build(ids).ApplyCursor(lastId: 0, size: size).ToList();

        Assert.Equal(expectedTake, page.Count);
    }

    [Fact]
    public void ApplyPage_SkipsAndTakesByIdDescending()
    {
        var ids = Enumerable.Range(1, 10).Select(value => (long)value).ToArray();

        var page = Build(ids).ApplyPage(PageRequest.Of(page: 2, size: 3)).ToList();

        // 倒序 10..1，第二页（skip 3 take 3）为 7,6,5。
        Assert.Equal(new long[] { 7, 6, 5 }, page.Select(entity => entity.Id));
    }

    [Fact]
    public void ApplyCursor_NullSource_Throws()
    {
        IQueryable<Sample> source = null!;

        Assert.Throws<ArgumentNullException>(() => source.ApplyCursor(0, 10));
    }
}
