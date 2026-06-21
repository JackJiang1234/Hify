using Hify.Contracts.Agent;
using Hify.Modules.Agent.Features.Agents;
using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;

namespace Hify.Modules.Agent.Endpoints;

/// <summary>Agent 配置管理接口。统一返回 <see cref="Result{T}"/>；入参校验由全局过滤器执行。</summary>
[ApiController]
[Route("api/v1/agents")]
internal sealed class AgentsController : ControllerBase
{
    private readonly AgentService _service;

    public AgentsController(AgentService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>创建 Agent（同事务建工具/知识库绑定）。</summary>
    [HttpPost]
    public Task<Result<AgentDto>> Create([FromBody] CreateAgentRequest request) =>
        _service.CreateAsync(request, HttpContext.RequestAborted);

    /// <summary>Agent 详情（含绑定 Id 列表）。</summary>
    [HttpGet("{id:long}")]
    public Task<Result<AgentDto>> Get(long id) =>
        _service.GetAsync(id, HttpContext.RequestAborted);

    /// <summary>分页列出 Agent。</summary>
    [HttpGet]
    public Task<PageResult<AgentDto>> List([FromQuery] int page = 1, [FromQuery] int size = 20) =>
        _service.ListAsync(page, size, HttpContext.RequestAborted);

    /// <summary>更新 Agent（绑定全量替换）。</summary>
    [HttpPut("{id:long}")]
    public Task<Result<AgentDto>> Update(long id, [FromBody] UpdateAgentRequest request) =>
        _service.UpdateAsync(id, request, HttpContext.RequestAborted);

    /// <summary>删除 Agent（级联软删工具/知识库绑定）。</summary>
    [HttpDelete("{id:long}")]
    public Task<Result<bool>> Delete(long id) =>
        _service.DeleteAsync(id, HttpContext.RequestAborted);

    /// <summary>启用 Agent。</summary>
    [HttpPost("{id:long}/enable")]
    public Task<Result<bool>> Enable(long id) =>
        _service.SetEnabledAsync(id, enabled: true, HttpContext.RequestAborted);

    /// <summary>停用 Agent。</summary>
    [HttpPost("{id:long}/disable")]
    public Task<Result<bool>> Disable(long id) =>
        _service.SetEnabledAsync(id, enabled: false, HttpContext.RequestAborted);
}
