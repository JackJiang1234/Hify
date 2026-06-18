namespace Hify.Modules.ModelProvider.Security;

/// <summary>
/// 供应商密钥的对称加解密。密文落库，明文仅在调用外部 API 拼请求时短暂解出，绝不入日志。
/// </summary>
internal interface ICredentialProtector
{
    /// <summary>加密明文密钥，返回密文（base64）。空串返回空串。</summary>
    /// <param name="plaintext">明文密钥。</param>
    string Protect(string plaintext);

    /// <summary>解密密文，返回明文。空串返回空串；密文被篡改或密钥不符将抛异常。</summary>
    /// <param name="cipherText">密文（base64）。</param>
    string Unprotect(string cipherText);
}
