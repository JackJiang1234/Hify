using System.Security.Cryptography;

using Hify.Shared.Security;

using Microsoft.Extensions.Options;

namespace Hify.Modules.ModelProvider.Tests.Security;

/// <summary>AES-GCM 凭证加解密：往返、非确定性、不泄露明文、防篡改、密钥校验。</summary>
public sealed class AesCredentialProtectorTests
{
    private static AesCredentialProtector CreateProtector()
    {
        var key = Convert.ToBase64String(new byte[32]); // AES-256 长度的测试密钥
        return new AesCredentialProtector(Options.Create(new CredentialProtectionOptions { Key = key }));
    }

    [Theory]
    [InlineData("sk-0123456789abcdef")]
    [InlineData("中文密钥-含 Unicode")]
    [InlineData("x")]
    [InlineData("")]
    public void ProtectThenUnprotect_RoundTripsOriginal(string plaintext)
    {
        var protector = CreateProtector();

        var cipher = protector.Protect(plaintext);
        var roundTripped = protector.Unprotect(cipher);

        Assert.Equal(plaintext, roundTripped);
    }

    [Fact]
    public void Protect_SamePlaintext_ProducesDifferentCipher()
    {
        var protector = CreateProtector();

        var first = protector.Protect("sk-secret-key");
        var second = protector.Protect("sk-secret-key");

        Assert.NotEqual(first, second); // 随机 nonce → 密文各异
    }

    [Fact]
    public void Protect_CipherDoesNotContainPlaintext()
    {
        var protector = CreateProtector();
        const string plaintext = "sk-super-secret-value";

        var cipher = protector.Protect(plaintext);

        Assert.DoesNotContain(plaintext, cipher, StringComparison.Ordinal);
    }

    [Fact]
    public void Unprotect_TamperedCipher_Throws()
    {
        var protector = CreateProtector();
        var cipher = protector.Protect("sk-secret-key");
        var bytes = Convert.FromBase64String(cipher);
        bytes[^1] ^= 0xFF; // 翻转末字节，破坏认证标签/密文
        var tampered = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(tampered));
    }

    [Fact]
    public void Constructor_MissingKey_Throws()
    {
        var options = Options.Create(new CredentialProtectionOptions { Key = string.Empty });

        Assert.Throws<InvalidOperationException>(() => new AesCredentialProtector(options));
    }

    [Fact]
    public void Constructor_InvalidKeyLength_Throws()
    {
        var options = Options.Create(new CredentialProtectionOptions
        {
            Key = Convert.ToBase64String(new byte[20]), // 非 16/24/32
        });

        Assert.Throws<InvalidOperationException>(() => new AesCredentialProtector(options));
    }
}
