using Hify.Modules.Conversation.Features.Chat;
using Hify.Modules.Conversation.Features.Context;
using Hify.Modules.Conversation.Features.Conversations;
using Hify.Modules.Conversation.Features.Retrieval;
using Hify.Modules.Conversation.Persistence;
using Hify.Shared.Configuration;
using Hify.Shared.Modularity;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Hify.Modules.Conversation;

/// <summary>
/// Conversation 模块注册入口（L2 编排层，依赖 Agent/ModelProvider/Knowledge/Mcp，仅经 Contracts）。
/// 负责对话引擎：流式响应（SSE）、多轮对话、上下文管理。一期纯文本，无工具循环。
/// 控制器与 FluentValidation 校验器由 Host 自动发现，无需在此注册。
/// </summary>
public sealed class ConversationModule : IModule
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 独立 DbContext / 独立 schema；连接串由全局 DatabaseOptions 构建。不启用 Migrations（DDL 手写）。
        services.AddDbContext<ConversationDbContext>((provider, options) =>
        {
            var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(BuildConnectionString(database));
        });

        // 无状态协作者注册为单例。
        services.AddSingleton<ITokenEstimator, CharBasedTokenEstimator>();
        services.AddSingleton<ConversationContextCache>();
        // RAG 检索：经 Contracts 调 Knowledge 模块（L2→L1）；检索失败由适配器内部降级。
        // Scoped——依赖的 IKnowledgeQuery 持有 Knowledge 的 Scoped DbContext。
        services.AddScoped<IRetriever, KnowledgeRetriever>();

        // 依赖 DbContext（Scoped）的应用服务与编排器。
        services.AddScoped<ContextBuilder>();
        services.AddScoped<ConversationOrchestrator>();
        services.AddScoped<ConversationService>();
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
