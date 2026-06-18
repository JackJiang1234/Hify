using System.ComponentModel.DataAnnotations;

namespace Hify.Modules.ModelProvider.Security;

/// <summary>
/// 凭证加密配置。<see cref="Key"/> 经 User Secrets（本地）/ 环境变量（生产）注入，绝不入仓库。
/// 密钥须跨重启稳定，否则既有密文无法解密。
/// </summary>
internal sealed class CredentialProtectionOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "ModelProvider:CredentialProtection";

    /// <summary>AES 密钥：base64 编码的 16/24/32 字节（分别对应 AES-128/192/256）。</summary>
    [Required]
    public string Key { get; set; } = string.Empty;
}
