using Hify.Shared.Results;

namespace Hify.Shared.Tests;

public class PageResultTests
{
    [Theory]
    [InlineData(0L, 1, 20)]
    [InlineData(100L, 2, 50)]
    [InlineData(9999L, 5, 100)]
    public void Ok_SetsCode200_AndPaginationFields(long total, int page, int size)
    {
        var list = new[] { "a", "b" };

        var result = PageResult<string>.Ok(list, total, page, size);

        Assert.Equal(200, result.Code);
        Assert.Equal("success", result.Message);
        Assert.Equal(list, result.Data);
        Assert.Equal(total, result.Total);
        Assert.Equal(page, result.Page);
        Assert.Equal(size, result.Size);
    }

    [Fact]
    public void Ok_NullList_NormalizesToEmpty_NotNull()
    {
        var result = PageResult<string>.Ok(null, total: 0, page: 1, size: 20);

        var data = result.Data;
        Assert.NotNull(data);
        Assert.Empty(data);
    }

    [Fact]
    public void Ok_IsAlsoAResult_ViaInheritance()
    {
        Result<IReadOnlyList<string>> result = PageResult<string>.Ok([], total: 0, page: 1, size: 20);

        Assert.Equal(200, result.Code);
    }
}
