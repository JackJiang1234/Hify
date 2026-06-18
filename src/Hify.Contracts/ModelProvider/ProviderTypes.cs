namespace Hify.Contracts.ModelProvider;

/// <summary>供应商类型常量（存储为 varchar，决定使用哪个适配器）。与前端、DDL 取值一一对齐。</summary>
public static class ProviderTypes
{
    /// <summary>OpenAI 及其兼容厂商（vLLM、LM Studio、多数国内厂商，改 base URL 即可复用）。</summary>
    public const string OpenAi = "openai";

    /// <summary>Anthropic Claude。</summary>
    public const string Claude = "claude";

    /// <summary>本地 Ollama。</summary>
    public const string Ollama = "ollama";
}
