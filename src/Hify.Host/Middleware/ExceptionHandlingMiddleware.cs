using Hify.Host.Json;
using Hify.Shared.Exceptions;
using Hify.Shared.Results;

using Newtonsoft.Json;

namespace Hify.Host.Middleware;

/// <summary>
/// 全局异常处理中间件：将异常统一转换为 <see cref="Result{T}"/> 响应。
/// <see cref="BizException"/>（可预期业务错误）返回 HTTP 200 + 业务错误码；
/// 其它未捕获异常返回 HTTP 500 + 系统内部错误，且不向客户端泄露异常细节。
/// </summary>
internal sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerSettings JsonSettings = HifyJsonSettings.Create();

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BizException ex)
        {
            _logger.LogWarning(ex, "业务异常 {Code}", ex.Code);
            await WriteAsync(context, StatusCodes.Status200OK, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未处理的服务器异常");
            await WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ErrorCode.InternalError.ToCode(),
                ErrorCode.InternalError.GetMessage());
        }
    }

    private static async Task WriteAsync(HttpContext context, int httpStatus, int code, string message)
    {
        // 响应已开始写出则无法再改写，放弃以免破坏已发送内容。
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = httpStatus;
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = Result<object>.Fail(code, message);
        var json = JsonConvert.SerializeObject(body, JsonSettings);
        await context.Response.WriteAsync(json, context.RequestAborted);
    }
}
