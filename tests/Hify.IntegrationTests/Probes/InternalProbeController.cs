using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;

namespace Hify.IntegrationTests.Probes;

/// <summary>
/// internal 控制器探针：验证 <c>InternalControllerFeatureProvider</c> 能让 MVC 发现并路由非 public 控制器。
/// </summary>
[ApiController]
[Route("__test/internal")]
internal sealed class InternalProbeController : ControllerBase
{
    [HttpGet]
    public Result<string> Get() => Result<string>.Ok("internal-ok");
}
