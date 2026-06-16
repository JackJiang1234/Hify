using Serilog.Core;
using Serilog.Events;

namespace Hify.Host.Logging;

/// <summary>
/// 敏感数据脱敏 enricher：按属性名（不区分大小写）将日志事件中的敏感字段值替换为掩码，
/// 落实「日志不输出 PII、凭证、完整提示词」规范。
/// 仅处理顶层结构化属性（如 <c>logger.LogInformation("{Password}", pwd)</c>）；
/// 对整体解构的对象仍应在调用处避免写入机密。
/// </summary>
internal sealed class SensitiveDataEnricher : ILogEventEnricher
{
    private const string Mask = "***";

    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "pwd",
        "secret",
        "token",
        "accesstoken",
        "refreshtoken",
        "apikey",
        "api_key",
        "authorization",
        "connectionstring",
        "prompt",
        "systemprompt",
        "messages",
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        foreach (var name in logEvent.Properties.Keys.ToArray())
        {
            if (SensitiveNames.Contains(name))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(name, Mask));
            }
        }
    }
}
