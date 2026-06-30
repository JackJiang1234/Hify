using Hify.Contracts.Mcp;
using Hify.Modules.Mcp.Features.Servers;
using Hify.Modules.Mcp.Features.Tools;
using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;

namespace Hify.Modules.Mcp.Endpoints;

/// <summary>MCP Server 管理接口。统一返回 <see cref="Result{T}"/>；入参校验由全局过滤器执行。</summary>
[ApiController]
[Route("api/v1/mcp-servers")]
internal sealed class McpServersController : ControllerBase
{
    private readonly McpServerService _service;
    private readonly McpConnectivityService _connectivity;
    private readonly McpToolSyncService _toolSync;
    private readonly McpToolService _tools;

    public McpServersController(
        McpServerService service,
        McpConnectivityService connectivity,
        McpToolSyncService toolSync,
        McpToolService tools)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(connectivity);
        ArgumentNullException.ThrowIfNull(toolSync);
        ArgumentNullException.ThrowIfNull(tools);
        _service = service;
        _connectivity = connectivity;
        _toolSync = toolSync;
        _tools = tools;
    }

    /// <summary>创建 MCP Server。</summary>
    [HttpPost]
    public Task<Result<McpServerDto>> Create([FromBody] CreateMcpServerRequest request) =>
        _service.CreateAsync(request, HttpContext.RequestAborted);

    /// <summary>MCP Server 详情。</summary>
    [HttpGet("{id:long}")]
    public Task<Result<McpServerDto>> Get(long id) =>
        _service.GetAsync(id, HttpContext.RequestAborted);

    /// <summary>分页列出 MCP Server。</summary>
    [HttpGet]
    public Task<PageResult<McpServerDto>> List([FromQuery] int page = 1, [FromQuery] int size = 20) =>
        _service.ListAsync(page, size, HttpContext.RequestAborted);

    /// <summary>更新 MCP Server（凭证留空则保留）。</summary>
    [HttpPut("{id:long}")]
    public Task<Result<McpServerDto>> Update(long id, [FromBody] UpdateMcpServerRequest request) =>
        _service.UpdateAsync(id, request, HttpContext.RequestAborted);

    /// <summary>删除 MCP Server（级联软删工具）。</summary>
    [HttpDelete("{id:long}")]
    public Task<Result<bool>> Delete(long id) =>
        _service.DeleteAsync(id, HttpContext.RequestAborted);

    /// <summary>启用 MCP Server。</summary>
    [HttpPost("{id:long}/enable")]
    public Task<Result<bool>> Enable(long id) =>
        _service.SetEnabledAsync(id, enabled: true, HttpContext.RequestAborted);

    /// <summary>停用 MCP Server。</summary>
    [HttpPost("{id:long}/disable")]
    public Task<Result<bool>> Disable(long id) =>
        _service.SetEnabledAsync(id, enabled: false, HttpContext.RequestAborted);

    /// <summary>连通性测试（initialize 握手），刷新连接状态。</summary>
    [HttpPost("{id:long}/test-connection")]
    public Task<Result<McpServerDto>> TestConnection(long id) =>
        _connectivity.TestConnectionAsync(id, HttpContext.RequestAborted);

    /// <summary>发现工具（tools/list）并原地 upsert。</summary>
    [HttpPost("{id:long}/sync-tools")]
    public Task<Result<McpServerDto>> SyncTools(long id) =>
        _toolSync.SyncToolsAsync(id, HttpContext.RequestAborted);

    /// <summary>列出该 Server 的工具（含 available/enabled）。</summary>
    [HttpGet("{id:long}/tools")]
    public Task<Result<IReadOnlyList<McpToolDto>>> ListTools(long id) =>
        _tools.ListByServerAsync(id, HttpContext.RequestAborted);

    /// <summary>清理该 Server 下服务端已移除（不可用）的工具，返回清理数量。</summary>
    [HttpPost("{id:long}/tools/prune")]
    public Task<Result<int>> PruneTools(long id) =>
        _tools.PruneRemovedToolsAsync(id, HttpContext.RequestAborted);
}
