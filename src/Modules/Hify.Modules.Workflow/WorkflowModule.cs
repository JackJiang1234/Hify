using Hify.Shared.Modularity;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hify.Modules.Workflow;

/// <summary>
/// Workflow 模块注册入口（L2 编排层，依赖 Agent/ModelProvider/Mcp，仅经 Contracts）。
/// 负责简版工作流（JSON 配置执行：线性 + 条件分支，不做可视化拖拽）。
/// </summary>
public sealed class WorkflowModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: 注册工作流 JSON 解析/执行引擎、DbContext（独立 schema）。
    }
}
