using Hify.Modules.Workflow.Features.Definitions;
using Hify.Modules.Workflow.Features.Execution;
using Hify.Modules.Workflow.Features.Execution.Nodes;
using Hify.Modules.Workflow.Features.Runs;
using Hify.Modules.Workflow.Persistence;
using Hify.Shared.Configuration;
using Hify.Shared.Modularity;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hify.Modules.Workflow;

/// <summary>
/// Workflow 模块注册入口（L2 编排层，依赖 Agent/ModelProvider/Mcp，仅经 Contracts）。
/// 负责简版工作流（JSON 配置执行：线性 + 单层条件分支 + 简单拖拽画布）。
/// 控制器与 FluentValidation 校验器由 Host 自动发现，无需在此注册。
/// </summary>
public sealed class WorkflowModule : IModule
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 独立 DbContext / 独立 schema；连接串由全局 DatabaseOptions 构建。不启用 Migrations（DDL 手写）。
        services.AddDbContext<WorkflowDbContext>((provider, options) =>
        {
            var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(BuildConnectionString(database));
        });

        // 无状态校验器/解析器注册为单例。
        services.AddSingleton<DefinitionValidator>();
        services.AddSingleton<VariableResolver>();

        // 节点执行器：引擎按 NodeType 分发。依赖 LLM/MCP 门面（其它模块注册），用 Scoped。
        services.AddScoped<INodeHandler, StartNodeHandler>();
        services.AddScoped<INodeHandler, LlmNodeHandler>();
        services.AddScoped<INodeHandler, ToolNodeHandler>();
        services.AddScoped<INodeHandler, ConditionNodeHandler>();
        services.AddScoped<INodeHandler, EndNodeHandler>();

        // 执行引擎：消费全部 INodeHandler。
        services.AddScoped<WorkflowEngine>();

        // 应用服务（依赖 Scoped DbContext）。
        services.AddScoped<WorkflowService>();
        services.AddScoped<WorkflowRunService>();
    }

    private static string BuildConnectionString(DatabaseOptions options) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Database,
            Username = options.Username,
            Password = options.Password,
            MaxPoolSize = options.MaxPoolSize,
            CommandTimeout = options.CommandTimeoutSeconds,
        }.ConnectionString;
}
