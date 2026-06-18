using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Features.Providers;
using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;

namespace Hify.Modules.ModelProvider.Endpoints;

/// <summary>供应商管理接口。统一返回 <see cref="Result{T}"/>；入参校验由全局过滤器执行。</summary>
[ApiController]
[Route("api/v1/providers")]
internal sealed class ProvidersController : ControllerBase
{
    private readonly ProviderService _service;
    private readonly ProviderConnectivityService _connectivity;

    public ProvidersController(ProviderService service, ProviderConnectivityService connectivity)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(connectivity);
        _service = service;
        _connectivity = connectivity;
    }

    /// <summary>创建供应商（同事务建健康行）。</summary>
    [HttpPost]
    public Task<Result<ProviderDto>> Create([FromBody] CreateProviderRequest request) =>
        _service.CreateAsync(request, HttpContext.RequestAborted);

    /// <summary>供应商详情（含当前健康）。</summary>
    [HttpGet("{id:long}")]
    public Task<Result<ProviderDto>> Get(long id) =>
        _service.GetAsync(id, HttpContext.RequestAborted);

    /// <summary>分页列出供应商（带健康）。</summary>
    [HttpGet]
    public Task<PageResult<ProviderDto>> List([FromQuery] int page = 1, [FromQuery] int size = 20) =>
        _service.ListAsync(page, size, HttpContext.RequestAborted);

    /// <summary>更新供应商（密钥留空则保留）。</summary>
    [HttpPut("{id:long}")]
    public Task<Result<ProviderDto>> Update(long id, [FromBody] UpdateProviderRequest request) =>
        _service.UpdateAsync(id, request, HttpContext.RequestAborted);

    /// <summary>删除供应商（级联软删模型与健康行）。</summary>
    [HttpDelete("{id:long}")]
    public Task<Result<bool>> Delete(long id) =>
        _service.DeleteAsync(id, HttpContext.RequestAborted);

    /// <summary>启用供应商。</summary>
    [HttpPost("{id:long}/enable")]
    public Task<Result<bool>> Enable(long id) =>
        _service.SetEnabledAsync(id, enabled: true, HttpContext.RequestAborted);

    /// <summary>停用供应商。</summary>
    [HttpPost("{id:long}/disable")]
    public Task<Result<bool>> Disable(long id) =>
        _service.SetEnabledAsync(id, enabled: false, HttpContext.RequestAborted);

    /// <summary>测试连通性并刷新健康状态。</summary>
    [HttpPost("{id:long}/test-connection")]
    public Task<Result<ProviderHealthDto>> TestConnection(long id) =>
        _connectivity.TestConnectionAsync(id, HttpContext.RequestAborted);
}
