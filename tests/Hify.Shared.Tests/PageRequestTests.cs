using Hify.Shared.Pagination;

namespace Hify.Shared.Tests;

public class PageRequestTests
{
    [Theory]
    [InlineData(1, 20, 1, 20)]      // 正常
    [InlineData(0, 20, 1, 20)]      // page<1 → 1
    [InlineData(-5, 20, 1, 20)]     // 负页码 → 1
    [InlineData(1, 0, 1, 20)]       // size<1 → 默认 20
    [InlineData(1, 1000, 1, 100)]   // size 超限 → 100
    [InlineData(3, 50, 3, 50)]      // 正常多页
    public void Of_NormalizesPageAndSize(int page, int size, int expectedPage, int expectedSize)
    {
        var request = PageRequest.Of(page, size);

        Assert.Equal(expectedPage, request.Page);
        Assert.Equal(expectedSize, request.Size);
    }

    [Theory]
    [InlineData(1, 20, 0)]          // 首页 skip 0
    [InlineData(2, 20, 20)]         // 第二页 skip 20
    [InlineData(3, 50, 100)]        // (3-1)*50
    public void Skip_ComputesOffset(int page, int size, int expectedSkip)
    {
        var request = PageRequest.Of(page, size);

        Assert.Equal(expectedSkip, request.Skip);
    }

    [Fact]
    public void Skip_CapsAtMaxOffset()
    {
        // (10000)*100 远超上限，应封顶到 MaxOffset。
        var request = PageRequest.Of(page: 10_000, size: 100);

        Assert.Equal(PageRequest.MaxOffset, request.Skip);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void IsFirstPage_ReflectsPage(int page, bool expected)
    {
        Assert.Equal(expected, PageRequest.Of(page, 20).IsFirstPage);
    }
}
