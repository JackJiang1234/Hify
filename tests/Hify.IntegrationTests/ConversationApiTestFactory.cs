using Hify.Contracts.ModelProvider;
using Hify.Shared.Results;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Npgsql;

namespace Hify.IntegrationTests;

/// <summary>
/// 对话 HTTP 集成测试用工厂：数据库指向测试 PG，注入凭证密钥并关探活；
/// 关键是把 <see cref="IModelInvoker"/> 替换为脚本化替身，使 SSE 端到端无需真实 LLM。
/// </summary>
public sealed class ConversationApiTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("HIFY_TEST_DB")
            ?? "Host=localhost;Port=5432;Database=hify;Username=hify;Password=hify";
        var parsed = new NpgsqlConnectionStringBuilder(connectionString);

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = parsed.Host,
                ["Database:Port"] = parsed.Port.ToString(),
                ["Database:Database"] = parsed.Database,
                ["Database:Username"] = parsed.Username,
                ["Database:Password"] = parsed.Password,
                ["Redis:Host"] = "localhost",
                ["ModelProvider:CredentialProtection:Key"] = Convert.ToBase64String(new byte[32]),
                ["ModelProvider:HealthProbe:Enabled"] = "false",
            }));

        builder.ConfigureTestServices(services =>
        {
            // 替换真实 LLM 调用门面为脚本化替身（流式吐固定片段）。
            services.RemoveAll<IModelInvoker>();
            services.AddScoped<IModelInvoker, ScriptedModelInvoker>();
        });
    }

    /// <summary>脚本化 LLM 替身：流式吐 "Hello" + ", " + "world!"，末片带用量。</summary>
    private sealed class ScriptedModelInvoker : IModelInvoker
    {
        public Task<Result<ChatResponse>> ChatAsync(long modelId, ChatRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<IAsyncEnumerable<ChatStreamChunk>>> ChatStreamAsync(long modelId, ChatRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Result<IAsyncEnumerable<ChatStreamChunk>>.Ok(Generate(cancellationToken)));

        public Task<Result<EmbeddingResponse>> EmbedAsync(long modelId, EmbeddingRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        private static async IAsyncEnumerable<ChatStreamChunk> Generate(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var delta in new[] { "Hello", ", ", "world!" })
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ChatStreamChunk { Delta = delta };
                await Task.Yield();
            }

            yield return new ChatStreamChunk { IsFinal = true, FinishReason = "stop", PromptTokens = 12, CompletionTokens = 3 };
        }
    }
}
