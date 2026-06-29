using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

namespace Hify.Shared.Security;

/// <summary>
/// 基于 AES-GCM 的凭证加解密。密钥从配置注入（跨重启稳定，避免 DataProtection 密钥环丢失导致无法解密）。
/// 密文布局：<c>nonce(12) || tag(16) || cipher</c> 整体 base64；每次加密用随机 nonce，故同一明文密文各异。
/// </summary>
public sealed class AesCredentialProtector : ICredentialProtector
{
    private const int NonceSize = 12; // AES-GCM 推荐 96-bit nonce
    private const int TagSize = 16;   // 128-bit 认证标签

    private readonly byte[] _key;

    /// <summary>构造，解码并校验配置中的密钥。密钥缺失/非法即抛出（在首次解析本服务时）。</summary>
    /// <param name="options">凭证加密配置。</param>
    public AesCredentialProtector(IOptions<CredentialProtectionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var raw = options.Value.Key;
        if (string.IsNullOrEmpty(raw))
        {
            throw new InvalidOperationException(
                $"未配置凭证加密密钥（{CredentialProtectionOptions.SectionName}:Key），无法保护凭证。");
        }

        _key = Convert.FromBase64String(raw);
        if (_key.Length is not (16 or 24 or 32))
        {
            throw new InvalidOperationException("凭证加密密钥长度非法：须为 16/24/32 字节的 base64 编码。");
        }
    }

    /// <inheritdoc />
    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        if (plaintext.Length == 0)
        {
            return string.Empty;
        }

        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        var output = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize + TagSize, cipher.Length);
        return Convert.ToBase64String(output);
    }

    /// <inheritdoc />
    public string Unprotect(string cipherText)
    {
        ArgumentNullException.ThrowIfNull(cipherText);
        if (cipherText.Length == 0)
        {
            return string.Empty;
        }

        var input = Convert.FromBase64String(cipherText);
        if (input.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("密文长度非法，无法解密。");
        }

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipher = new byte[input.Length - NonceSize - TagSize];
        Buffer.BlockCopy(input, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(input, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(input, NonceSize + TagSize, cipher, 0, cipher.Length);

        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(_key, TagSize))
        {
            // 密文/标签被篡改或密钥不符时抛 AuthenticationTagMismatchException（CryptographicException 派生）。
            aes.Decrypt(nonce, cipher, tag, plain);
        }

        return Encoding.UTF8.GetString(plain);
    }
}
