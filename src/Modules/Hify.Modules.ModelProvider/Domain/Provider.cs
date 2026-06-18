using Hify.Shared.Persistence;

namespace Hify.Modules.ModelProvider.Domain;

/// <summary>
/// 供应商实例：一份 OpenAI/Claude/Ollama 接入配置。
/// 鉴权差异统一为「注入方式 <see cref="AuthType"/> + 头名 <see cref="AuthHeaderName"/> + 密文 <see cref="ApiKeyCipher"/>」，
/// 各家私有静态配置放 <see cref="Settings"/>（jsonb）。健康状态另存 provider_health 表（1:1）。
/// </summary>
internal sealed class Provider : EntityBase
{
    /// <summary>用户可见名称（同一未删集合内唯一）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>供应商类型，决定适配器：<c>openai</c> | <c>claude</c> | <c>ollama</c>。</summary>
    public string ProviderType { get; set; } = string.Empty;

    /// <summary>API 基址（兼容厂商/Ollama 必填）。</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>鉴权注入方式：<c>none</c> | <c>bearer</c> | <c>header</c>。</summary>
    public string AuthType { get; set; } = "none";

    /// <summary><c>header</c> 模式下的请求头名（如 <c>x-api-key</c>）；其余模式为空。</summary>
    public string AuthHeaderName { get; set; } = string.Empty;

    /// <summary>加密后的密钥，绝不存明文、绝不入日志。</summary>
    public string ApiKeyCipher { get; set; } = string.Empty;

    /// <summary>密钥末位明文，仅供 UI 展示（如 <c>sk-…a1b2</c>）。</summary>
    public string ApiKeyHint { get; set; } = string.Empty;

    /// <summary>私有静态配置（jsonb），如 <c>anthropic-version</c>、<c>organization</c>。</summary>
    public string Settings { get; set; } = "{}";

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; } = true;
}
