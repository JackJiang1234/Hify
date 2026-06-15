using Hify.Shared.Exceptions;

namespace Hify.Shared.Tests;

public class ErrorCodeTests
{
    public static IEnumerable<object[]> AllCodes() =>
        Enum.GetValues<ErrorCode>().Select(code => new object[] { code });

    [Theory]
    [MemberData(nameof(AllCodes))]
    public void EveryCode_IsInGenericRange_AndHasMessage(ErrorCode code)
    {
        Assert.InRange(code.ToCode(), 1000, 1999);
        Assert.False(string.IsNullOrWhiteSpace(code.GetMessage()));
    }

    [Theory]
    [InlineData(ErrorCode.InternalError, 1000)]
    [InlineData(ErrorCode.ParamInvalid, 1001)]
    [InlineData(ErrorCode.Unauthorized, 1002)]
    [InlineData(ErrorCode.Forbidden, 1003)]
    [InlineData(ErrorCode.NotFound, 1004)]
    [InlineData(ErrorCode.Conflict, 1005)]
    [InlineData(ErrorCode.TooManyRequests, 1006)]
    [InlineData(ErrorCode.Timeout, 1007)]
    public void ToCode_ReturnsExpectedNumericValue(ErrorCode code, int expected)
    {
        Assert.Equal(expected, code.ToCode());
    }
}
