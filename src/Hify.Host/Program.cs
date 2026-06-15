// 启动项目入口（组合根）。
// 骨架阶段：装配各模块（含全局 Newtonsoft JSON 与控制器）、全局异常处理、路由；后续接入 Swagger。
using Hify.Host.Configuration;
using Hify.Host.HealthChecks;
using Hify.Host.Middleware;
using Hify.Host.Modularity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHifyConfiguration(builder.Configuration);
builder.Services.AddHifyHealthChecks();
builder.Services.AddHifyModules(builder.Configuration);

var app = builder.Build();

// 全局异常处理：须位于管道最前，统一捕获后续所有异常并转为 Result。
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapControllers();
app.MapHifyHealthChecks();

app.Run();

// 供集成测试（WebApplicationFactory<Program>）引用。
public partial class Program
{
}
