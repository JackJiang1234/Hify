using System.ComponentModel.DataAnnotations;

namespace Hify.Shared.Configuration;

/// <summary>
/// Redis 连接配置。非敏感项放 appsettings.json；<see cref="Password"/> 经
/// User Secrets（本地）或环境变量（生产）注入，不入仓库。
/// </summary>
public sealed class RedisOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "Redis";

    /// <summary>主机名或地址。生产环境为内部主机名，须经私密配置注入。</summary>
    [Required]
    public string Host { get; set; } = "";

    /// <summary>端口。</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 6379;

    /// <summary>密码（敏感）。本地无认证时可留空；有认证时经私密配置注入。</summary>
    public string Password { get; set; } = "";

    /// <summary>逻辑库索引。</summary>
    [Range(0, 15)]
    public int Database { get; set; } = 0;

    /// <summary>连接超时（毫秒）。所有外部调用须设超时。</summary>
    [Range(100, 60000)]
    public int ConnectTimeoutMs { get; set; } = 5000;
}
