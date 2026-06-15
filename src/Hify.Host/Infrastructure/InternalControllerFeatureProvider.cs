using System.Reflection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Hify.Host.Infrastructure;

/// <summary>
/// 让 MVC 能发现 internal 控制器（默认仅发现 public）。
/// 配合 CLAUDE.md「默认 internal」约定：模块内控制器无需 public 即可被路由。
/// </summary>
internal sealed class InternalControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo)
    {
        if (!typeInfo.IsClass || typeInfo.IsAbstract || typeInfo.ContainsGenericParameters)
        {
            return false;
        }

        if (typeInfo.IsDefined(typeof(NonControllerAttribute)))
        {
            return false;
        }

        var hasSuffix = typeInfo.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase);
        var hasAttribute = typeInfo.IsDefined(typeof(ControllerAttribute));
        return hasSuffix || hasAttribute;
    }
}
