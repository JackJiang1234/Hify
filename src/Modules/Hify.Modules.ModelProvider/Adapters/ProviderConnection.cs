using Hify.Contracts.ModelProvider;

namespace Hify.Modules.ModelProvider.Adapters;

/// <summary>
/// 已解析的供应商连接信息，含解密后的明文密钥（仅在内存短暂存在，绝不入日志）。
/// 由功能层从 provider 行解密构建后传入适配器。
/// </summary>
internal sealed record ProviderConnection
{
    /// <summary>供应商类型，见 <see cref="ProviderTypes"/>。</summary>
    public string ProviderType { get; init; } = string.Empty;

    /// <summary>API 基址。</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>鉴权方式，见 <see cref="AuthTypes"/>。</summary>
    public string AuthType { get; init; } = AuthTypes.None;

    /// <summary><c>header</c> 鉴权下的头名。</summary>
    public string AuthHeaderName { get; init; } = string.Empty;

    /// <summary>明文密钥。</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>私有静态配置（JSON 头映射）。</summary>
    public string Settings { get; init; } = "{}";
}
