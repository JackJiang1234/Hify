using Hify.Host.Json;
using Hify.Shared.Exceptions;
using Hify.Shared.Results;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Newtonsoft.Json;

namespace Hify.Host.HealthChecks;

/// <summary>
/// 将健康检查报告写为统一 <see cref="Result{T}"/> 响应体（Newtonsoft，camelCase）。
/// HTTP 状态码由 HealthChecks 中间件按健康状态设置（健康 200 / 不健康 503），此处仅写出 body。
/// </summary>
internal static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerSettings JsonSettings = HifyJsonSettings.Create();

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var checks = report.Entries
            .Select(entry => new HealthEntryDto(
                entry.Key,
                entry.Value.Status.ToString(),
                entry.Value.Description ?? ""))
            .ToList();

        var data = new HealthReportDto(
            report.Status.ToString(),
            (long)report.TotalDuration.TotalMilliseconds,
            checks);

        var healthy = report.Status == HealthStatus.Healthy;
        var body = new Result<HealthReportDto>
        {
            Code = healthy ? 200 : ErrorCode.InternalError.ToCode(),
            Message = healthy ? "healthy" : "unhealthy",
            Data = data,
        };

        var json = JsonConvert.SerializeObject(body, JsonSettings);
        return context.Response.WriteAsync(json, context.RequestAborted);
    }

    private sealed record HealthReportDto(string Status, long TotalDurationMs, IReadOnlyList<HealthEntryDto> Checks);

    private sealed record HealthEntryDto(string Name, string Status, string Description);
}
