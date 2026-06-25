using Hify.Contracts.ModelProvider;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Npgsql;

namespace Hify.IntegrationTests;

/// <summary>
/// 知识库 HTTP 集成测试用工厂：数据库指向测试 PG，注入凭证密钥、关闭周期探活；
/// 并把 <see cref="IModelInvoker"/> 替换为 <see cref="StubModelInvoker"/>——上传/检索的嵌入调用不触网。
/// </summary>
public sealed class KnowledgeApiTestFactory : WebApplicationFactory<Program>
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

        // 替换外部 LLM 调用边界：嵌入用桩，避免集成测试触达真实 provider。
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IModelInvoker>();
            services.AddScoped<IModelInvoker, StubModelInvoker>();
        });
    }
}
