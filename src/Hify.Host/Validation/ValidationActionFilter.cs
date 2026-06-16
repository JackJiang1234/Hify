using FluentValidation;

using Hify.Shared.Exceptions;
using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hify.Host.Validation;

/// <summary>
/// 全局校验过滤器：对每个 action 参数解析对应的 FluentValidation 校验器并执行。
/// 校验不通过时短路请求，返回统一 <see cref="Result{T}"/>（业务码 <see cref="ErrorCode.ParamInvalid"/>），
/// 不进入 action；未注册校验器的参数直接放行。
/// </summary>
internal sealed class ValidationActionFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;

    public ValidationActionFilter(IServiceProvider services)
    {
        _services = services;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (result.IsValid)
            {
                continue;
            }

            var message = string.Join("; ", result.Errors.Select(error => error.ErrorMessage));
            context.Result = new ObjectResult(
                Result<object>.Fail(ErrorCode.ParamInvalid.ToCode(), message))
            {
                StatusCode = StatusCodes.Status200OK,
            };
            return;
        }

        await next();
    }
}
