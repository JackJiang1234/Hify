using System.Reflection;

using FluentValidation;

using Hify.Shared.Exceptions;
using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;

namespace Hify.Host.Validation;

/// <summary>
/// 校验管道装配：注册各模块校验器，并接管 [ApiController] 默认的模型绑定/校验失败响应，
/// 使其与统一 <see cref="Result{T}"/> 契约（业务码 <see cref="ErrorCode.ParamInvalid"/>）对齐，
/// 而非默认的 <c>ProblemDetails</c> 400。
/// </summary>
internal static class ValidationHostExtensions
{
    /// <summary>扫描指定程序集注册其中的 FluentValidation 校验器（含 internal 类型）。</summary>
    /// <param name="services">DI 服务集合。</param>
    /// <param name="assembly">模块程序集。</param>
    public static IServiceCollection AddHifyValidators(this IServiceCollection services, Assembly assembly)
    {
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }

    /// <summary>将模型绑定/校验失败（[ApiController] 自动 400）改为统一 Result（HTTP 200 + 业务码 1001）。</summary>
    /// <param name="services">DI 服务集合。</param>
    public static IServiceCollection AddHifyModelStateHandling(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var message = string.Join(
                    "; ",
                    context.ModelState.Values
                        .SelectMany(state => state.Errors)
                        .Select(error => error.ErrorMessage)
                        .Where(text => !string.IsNullOrWhiteSpace(text)));

                if (string.IsNullOrWhiteSpace(message))
                {
                    message = ErrorCode.ParamInvalid.GetMessage();
                }

                return new ObjectResult(
                    Result<object>.Fail(ErrorCode.ParamInvalid.ToCode(), message))
                {
                    StatusCode = StatusCodes.Status200OK,
                };
            };
        });

        return services;
    }
}
