// 启动项目入口（组合根）。
// 装配各模块（含全局 Newtonsoft JSON 与控制器）、Serilog 结构化日志、全局异常处理、路由。
using Hify.Host.Configuration;
using Hify.Host.HealthChecks;
using Hify.Host.Logging;
using Hify.Host.Middleware;
using Hify.Host.Modularity;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 经 DI 注册 Serilog（不使用全局静态 Log.Logger，避免并行集成测试中多宿主共享静态状态）。
// 从配置读取级别与 sink，叠加 LogContext 与敏感数据脱敏。
builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.With<SensitiveDataEnricher>());

builder.Services.AddHifyConfiguration(builder.Configuration);
builder.Services.AddHifyHealthChecks();
builder.Services.AddHifyModules(builder.Configuration);

var app = builder.Build();

// 请求日志（含 TraceId，便于跨日志关联）。置于最外层以覆盖整条管道耗时。
app.UseSerilogRequestLogging(options =>
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier));

// 全局异常处理：统一捕获后续所有异常并转为 Result。
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapControllers();
app.MapHifyHealthChecks();

app.Run();

// 供集成测试（WebApplicationFactory<Program>）引用。
public partial class Program
{
}
