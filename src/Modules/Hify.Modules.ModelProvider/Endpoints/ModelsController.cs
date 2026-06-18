using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Features.Models;
using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;

namespace Hify.Modules.ModelProvider.Endpoints;

/// <summary>
/// 模型管理接口。创建/列表挂在供应商下；单模型操作用扁平 <c>/api/v1/models/{id}</c>。统一返回 <see cref="Result{T}"/>。
/// </summary>
[ApiController]
internal sealed class ModelsController : ControllerBase
{
    private readonly ModelService _service;

    public ModelsController(ModelService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>在供应商下新增模型（手动录入）。</summary>
    [HttpPost("api/v1/providers/{providerId:long}/models")]
    public Task<Result<ModelDto>> Create(long providerId, [FromBody] CreateModelRequest request) =>
        _service.CreateAsync(providerId, request, HttpContext.RequestAborted);

    /// <summary>列出供应商下的模型。</summary>
    [HttpGet("api/v1/providers/{providerId:long}/models")]
    public Task<Result<IReadOnlyList<ModelDto>>> List(long providerId) =>
        _service.ListByProviderAsync(providerId, HttpContext.RequestAborted);

    /// <summary>模型详情。</summary>
    [HttpGet("api/v1/models/{id:long}")]
    public Task<Result<ModelDto>> Get(long id) =>
        _service.GetAsync(id, HttpContext.RequestAborted);

    /// <summary>更新模型。</summary>
    [HttpPut("api/v1/models/{id:long}")]
    public Task<Result<ModelDto>> Update(long id, [FromBody] UpdateModelRequest request) =>
        _service.UpdateAsync(id, request, HttpContext.RequestAborted);

    /// <summary>删除模型（软删）。</summary>
    [HttpDelete("api/v1/models/{id:long}")]
    public Task<Result<bool>> Delete(long id) =>
        _service.DeleteAsync(id, HttpContext.RequestAborted);

    /// <summary>设为该供应商该类型的默认模型。</summary>
    [HttpPost("api/v1/models/{id:long}/set-default")]
    public Task<Result<bool>> SetDefault(long id) =>
        _service.SetDefaultAsync(id, HttpContext.RequestAborted);

    /// <summary>启用模型。</summary>
    [HttpPost("api/v1/models/{id:long}/enable")]
    public Task<Result<bool>> Enable(long id) =>
        _service.SetEnabledAsync(id, enabled: true, HttpContext.RequestAborted);

    /// <summary>停用模型。</summary>
    [HttpPost("api/v1/models/{id:long}/disable")]
    public Task<Result<bool>> Disable(long id) =>
        _service.SetEnabledAsync(id, enabled: false, HttpContext.RequestAborted);
}
