using FluentValidation;

using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;

namespace Hify.IntegrationTests.Probes;

/// <summary>
/// 校验管道探针：POST 一个带 FluentValidation 校验器的请求体，
/// 验证全局校验过滤器会在校验失败时短路并返回统一 Result（业务码 1001）。
/// </summary>
[ApiController]
[Route("__test/validation")]
public sealed class ValidationProbeController : ControllerBase
{
    [HttpPost]
    public Result<CreateThingRequest> Post([FromBody] CreateThingRequest request) =>
        Result<CreateThingRequest>.Ok(request);
}

/// <summary>探针请求体。</summary>
public sealed record CreateThingRequest
{
    /// <summary>名称，必填非空。</summary>
    public string Name { get; init; } = "";

    /// <summary>数量，必须大于 0。</summary>
    public int Count { get; init; }
}

/// <summary>探针请求校验器。</summary>
internal sealed class CreateThingRequestValidator : AbstractValidator<CreateThingRequest>
{
    public CreateThingRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage("name 不能为空");
        RuleFor(request => request.Count).GreaterThan(0).WithMessage("count 必须大于 0");
    }
}
