using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;

namespace Hify.IntegrationTests.Probes;

/// <summary>
/// 仅用于集成测试的探针控制器：经真实 MVC 管道返回 <see cref="Result{T}"/>，
/// 验证全局 Newtonsoft 序列化（camelCase、保留 null）确实生效。
/// </summary>
[ApiController]
[Route("__test/json")]
public sealed class JsonProbeController : ControllerBase
{
    [HttpGet]
    public Result<Payload> Get() => Result<Payload>.Ok(new Payload());

    public sealed record Payload
    {
        public string DisplayName { get; init; } = "hify";

        public string? Note { get; init; }
    }
}
