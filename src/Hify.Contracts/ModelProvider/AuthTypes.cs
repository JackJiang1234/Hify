namespace Hify.Contracts.ModelProvider;

/// <summary>鉴权注入方式常量。</summary>
public static class AuthTypes
{
    /// <summary>无鉴权（如本地 Ollama）。</summary>
    public const string None = "none";

    /// <summary><c>Authorization: Bearer &lt;key&gt;</c>（OpenAI 系）。</summary>
    public const string Bearer = "bearer";

    /// <summary>自定义请求头注入密钥（如 Claude 的 <c>x-api-key</c>）。</summary>
    public const string Header = "header";
}
