using Hify.Modules.Mcp.Features.Tools;
using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;

namespace Hify.Modules.Mcp.Endpoints;

/// <summary>MCP 工具管理接口（启停单个工具）。统一返回 <see cref="Result{T}"/>。</summary>
[ApiController]
[Route("api/v1/mcp-tools")]
internal sealed class McpToolsController : ControllerBase
{
    private readonly McpToolService _service;

    public McpToolsController(McpToolService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>启用工具。</summary>
    [HttpPost("{id:long}/enable")]
    public Task<Result<bool>> Enable(long id) =>
        _service.SetToolEnabledAsync(id, enabled: true, HttpContext.RequestAborted);

    /// <summary>停用工具。</summary>
    [HttpPost("{id:long}/disable")]
    public Task<Result<bool>> Disable(long id) =>
        _service.SetToolEnabledAsync(id, enabled: false, HttpContext.RequestAborted);
}
