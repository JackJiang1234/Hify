using Hify.Modules.Knowledge.Persistence;
using Hify.Shared.Configuration;
using Hify.Shared.Modularity;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Npgsql;

using Pgvector.EntityFrameworkCore;

namespace Hify.Modules.Knowledge;

/// <summary>
/// Knowledge 模块注册入口（L1 领域能力，依赖 ModelProvider 计算 embedding，仅经 Contracts）。
/// 负责知识库与 RAG（一期 TXT 文档、固定长度分块、pgvector 检索）。
/// 控制器与 FluentValidation 校验器由 Host 自动发现，无需在此注册。
/// </summary>
public sealed class KnowledgeModule : IModule
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 独立 DbContext / 独立 schema；连接串由全局 DatabaseOptions 构建。不启用 Migrations（DDL 手写）。
        services.AddDbContext<KnowledgeDbContext>((provider, options) =>
        {
            var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            // UseVector 注册 pgvector 类型映射（vector 列 <-> Pgvector.Vector）。
            options.UseNpgsql(BuildConnectionString(database), npgsql => npgsql.UseVector());
        });

        // 知识库配置服务（依赖 DbContext + 跨模块 IModelProviderQuery，注册为 Scoped）。
        services.AddScoped<Features.KnowledgeBases.KnowledgeBaseService>();

        // 文档上传服务（依赖 DbContext + 跨模块 IModelInvoker，注册为 Scoped）。
        services.AddScoped<Features.Documents.DocumentService>();

        // 跨模块只读检索：供对话引擎（L2）RAG 装配，对接 Conversation 的 IRetriever seam。
        services.AddScoped<Hify.Contracts.Knowledge.IKnowledgeQuery, Features.Search.KnowledgeQuery>();
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
