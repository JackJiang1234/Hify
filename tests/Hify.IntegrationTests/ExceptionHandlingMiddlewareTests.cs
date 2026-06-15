using System.Text;
using System.Text.Json;

using Hify.Host.Middleware;
using Hify.Shared.Exceptions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hify.IntegrationTests;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int Status, JsonElement Body)> InvokeAsync(Exception thrown)
    {
        var context = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        RequestDelegate next = _ => throw thrown;
        var middleware = new ExceptionHandlingMiddleware(next, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        responseBody.Seek(0, SeekOrigin.Begin);
        var text = await new StreamReader(responseBody, Encoding.UTF8).ReadToEndAsync();
        using var doc = JsonDocument.Parse(text);
        return (context.Response.StatusCode, doc.RootElement.Clone());
    }

    [Theory]
    [InlineData(ErrorCode.NotFound, 1004)]
    [InlineData(ErrorCode.Unauthorized, 1002)]
    public async Task BizException_ReturnsHttp200_WithBusinessCodeAndDefaultMessage(ErrorCode code, int expected)
    {
        var (status, body) = await InvokeAsync(new BizException(code));

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(expected, body.GetProperty("code").GetInt32());
        Assert.Equal(code.GetMessage(), body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task BizException_PreservesCustomMessage()
    {
        var (status, body) = await InvokeAsync(new BizException(ErrorCode.ParamInvalid, "name 必填"));

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(1001, body.GetProperty("code").GetInt32());
        Assert.Equal("name 必填", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UnexpectedException_ReturnsHttp500_AsInternalError_WithoutLeakingDetails()
    {
        var (status, body) = await InvokeAsync(new InvalidOperationException("boom-secret"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal(1000, body.GetProperty("code").GetInt32());
        Assert.Equal(ErrorCode.InternalError.GetMessage(), body.GetProperty("message").GetString());
        Assert.NotEqual("boom-secret", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task SerializedBody_UsesCamelCaseKeys()
    {
        var (_, body) = await InvokeAsync(new BizException(ErrorCode.NotFound));

        Assert.True(body.TryGetProperty("code", out _));
        Assert.True(body.TryGetProperty("message", out _));
        Assert.True(body.TryGetProperty("data", out _));
    }
}
