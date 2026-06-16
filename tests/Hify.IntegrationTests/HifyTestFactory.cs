using FluentValidation;

using Hify.IntegrationTests.Probes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hify.IntegrationTests;

/// <summary>
/// 集成测试用工厂：注入最小有效配置（含密码，使启动校验通过，无需真实数据库/Redis），
/// 并将测试探针控制器纳入控制器发现范围。
/// </summary>
public sealed class HifyTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = "localhost",
                ["Database:Database"] = "hify_test",
                ["Database:Username"] = "hify",
                ["Database:Password"] = "test-secret",
                ["Redis:Host"] = "localhost",
            }));

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(JsonProbeController).Assembly);
            // 注册探针程序集内的校验器（生产环境只扫描各业务模块程序集，不含测试程序集）。
            services.AddValidatorsFromAssemblyContaining<ValidationProbeController>(includeInternalTypes: true);
        });
    }
}
