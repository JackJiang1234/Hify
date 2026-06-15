using Hify.Shared.Modularity;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hify.Modules.Mcp;

/// <summary>
/// MCP 模块注册入口（L0 基础能力，不依赖任何业务模块）。
/// 负责通过 MCP 协议接入与调用外部工具。
/// </summary>
public sealed class McpModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: 注册 MCP 客户端、工具发现/调用服务、DbContext（独立 schema）。
    }
}
