namespace Hify.Shared.Security;

/// <summary>
/// 凭证的对称加解密（app 级共享基建）。密文落库，明文仅在调用外部 API 拼请求时短暂解出，绝不入日志。
/// 供需要保护密钥的模块（ModelProvider、Mcp 等）共用同一套实现与密钥。
/// </summary>
public interface ICredentialProtector
{
    /// <summary>加密明文密钥，返回密文（base64）。空串返回空串。</summary>
    /// <param name="plaintext">明文密钥。</param>
    string Protect(string plaintext);

    /// <summary>解密密文，返回明文。空串返回空串；密文被篡改或密钥不符将抛异常。</summary>
    /// <param name="cipherText">密文（base64）。</param>
    string Unprotect(string cipherText);
}
