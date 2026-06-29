using Hify.Shared.Security;

using Microsoft.Extensions.Options;

namespace Hify.Modules.ModelProvider.Tests.Support;

/// <summary>测试用凭证加密器（固定 32 字节 AES-256 密钥；同密钥的不同实例可互相解密）。</summary>
internal static class TestProtector
{
    public static AesCredentialProtector Create() =>
        new(Options.Create(new CredentialProtectionOptions { Key = Convert.ToBase64String(new byte[32]) }));
}
