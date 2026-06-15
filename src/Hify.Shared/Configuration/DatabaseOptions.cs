using System.ComponentModel.DataAnnotations;

namespace Hify.Shared.Configuration;

/// <summary>
/// PostgreSQL 连接配置。非敏感项放 appsettings.json；<see cref="Password"/> 等敏感项经
/// User Secrets（本地）或环境变量（生产）注入，不入仓库。
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "Database";

    /// <summary>主机名或地址。生产环境为内部主机名，须经私密配置注入。</summary>
    [Required]
    public string Host { get; set; } = "";

    /// <summary>端口。</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 5432;

    /// <summary>数据库名。</summary>
    [Required]
    public string Database { get; set; } = "";

    /// <summary>用户名。</summary>
    [Required]
    public string Username { get; set; } = "";

    /// <summary>密码（敏感）。仅经 User Secrets / 环境变量注入，不写入 appsettings。</summary>
    [Required]
    public string Password { get; set; } = "";

    /// <summary>连接池最大连接数。</summary>
    [Range(1, 1000)]
    public int MaxPoolSize { get; set; } = 50;

    /// <summary>命令超时（秒）。所有外部调用须设超时。</summary>
    [Range(1, 600)]
    public int CommandTimeoutSeconds { get; set; } = 30;
}
