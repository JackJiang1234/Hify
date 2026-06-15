using Hify.Shared.Results;

namespace Hify.Shared.Tests;

public class ResultTests
{
    [Theory]
    [InlineData("hello", "success")]
    [InlineData("", "success")]
    [InlineData("payload", "created")]
    public void Ok_SetsCode200_AndCarriesData(string data, string message)
    {
        var result = Result<string>.Ok(data, message);

        Assert.Equal(200, result.Code);
        Assert.Equal(message, result.Message);
        Assert.Equal(data, result.Data);
    }

    [Fact]
    public void Ok_UsesSuccessMessage_ByDefault()
    {
        var result = Result<int>.Ok(42);

        Assert.Equal(200, result.Code);
        Assert.Equal("success", result.Message);
        Assert.Equal(42, result.Data);
    }

    [Theory]
    [InlineData(1000, "bad request")]
    [InlineData(2001, "provider not found")]
    [InlineData(3002, "agent disabled")]
    public void Fail_SetsCodeAndMessage_WithNullData(int code, string message)
    {
        var result = Result<string>.Fail(code, message);

        Assert.Equal(code, result.Code);
        Assert.Equal(message, result.Message);
        Assert.Null(result.Data);
    }
}
