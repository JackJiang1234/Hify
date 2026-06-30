using Hify.Modules.Workflow.Features.Definitions;
using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;

namespace Hify.Modules.Workflow.Endpoints;

/// <summary>工作流管理接口（CRUD + 发布）。统一返回 <see cref="Result{T}"/>；入参校验由全局过滤器执行。</summary>
[ApiController]
[Route("api/v1/workflows")]
internal sealed class WorkflowsController : ControllerBase
{
    private readonly WorkflowService _service;

    public WorkflowsController(WorkflowService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>新建工作流（草稿）。</summary>
    [HttpPost]
    public Task<Result<WorkflowDto>> Create([FromBody] CreateWorkflowRequest request) =>
        _service.CreateAsync(request, HttpContext.RequestAborted);

    /// <summary>分页列出工作流（按 id 倒序）。</summary>
    [HttpGet]
    public Task<PageResult<WorkflowDto>> List([FromQuery] int page = 1, [FromQuery] int size = 20) =>
        _service.ListAsync(page, size, HttpContext.RequestAborted);

    /// <summary>取工作流详情（含 definition）。</summary>
    [HttpGet("{id:long}")]
    public Task<Result<WorkflowDto>> Get(long id) =>
        _service.GetAsync(id, HttpContext.RequestAborted);

    /// <summary>更新工作流（定义改动会退回 draft）。</summary>
    [HttpPut("{id:long}")]
    public Task<Result<WorkflowDto>> Update(long id, [FromBody] UpdateWorkflowRequest request) =>
        _service.UpdateAsync(id, request, HttpContext.RequestAborted);

    /// <summary>发布工作流（发布前跑图校验，不过返回 6002）。</summary>
    [HttpPost("{id:long}/publish")]
    public Task<Result<WorkflowDto>> Publish(long id) =>
        _service.PublishAsync(id, HttpContext.RequestAborted);

    /// <summary>删除工作流（软删）。</summary>
    [HttpDelete("{id:long}")]
    public Task<Result<bool>> Delete(long id) =>
        _service.DeleteAsync(id, HttpContext.RequestAborted);
}
