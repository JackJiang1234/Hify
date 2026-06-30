using Hify.Modules.Workflow.Features.Runs;
using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;

namespace Hify.Modules.Workflow.Endpoints;

/// <summary>工作流执行接口（试运行 + 运行记录）。同步执行，跑完返回。统一返回 <see cref="Result{T}"/>。</summary>
[ApiController]
[Route("api/v1/workflows/{workflowId:long}/runs")]
internal sealed class WorkflowRunsController : ControllerBase
{
    private readonly WorkflowRunService _service;

    public WorkflowRunsController(WorkflowRunService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>试运行（同步执行，body 带 inputs）。执行失败仍返回 run（status=failed + trace）。</summary>
    [HttpPost]
    public Task<Result<WorkflowRunDto>> Run(long workflowId, [FromBody] CreateRunRequest request) =>
        _service.RunAsync(workflowId, request, HttpContext.RequestAborted);

    /// <summary>分页列出运行记录（按 id 倒序）。</summary>
    [HttpGet]
    public Task<PageResult<WorkflowRunDto>> List(long workflowId, [FromQuery] int page = 1, [FromQuery] int size = 20) =>
        _service.ListAsync(workflowId, page, size, HttpContext.RequestAborted);

    /// <summary>取运行详情（含逐节点 trace）。</summary>
    [HttpGet("{runId:long}")]
    public Task<Result<WorkflowRunDto>> Get(long workflowId, long runId) =>
        _service.GetAsync(workflowId, runId, HttpContext.RequestAborted);
}
