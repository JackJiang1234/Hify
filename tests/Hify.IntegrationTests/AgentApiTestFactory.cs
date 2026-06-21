using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

using Npgsql;

namespace Hify.IntegrationTests;

/// <summary>
/// Agent HTTP 集成测试用工厂：把数据库指向测试 PG（连接串读 HIFY_TEST_DB，默认本地 5432），
/// 注入凭证加密密钥（ModelProvider 模块所需），并关闭周期探活避免后台噪声。
/// </summary>
public sealed class AgentApiTestFactory : WebApplicationFactory<Program>
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
    }
}
