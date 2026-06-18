namespace Hify.Contracts.ModelProvider;

/// <summary>
/// 供应商（脱敏视图）。绝不暴露密钥密文，仅以 <see cref="ApiKeyHint"/> 展示末位。
/// 供模块间引用与管理 API 返回共用。
/// </summary>
public record ProviderDto
{
    /// <summary>主键。</summary>
    public long Id { get; init; }

    /// <summary>名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>供应商类型，见 <see cref="ProviderTypes"/>。</summary>
    public string ProviderType { get; init; } = string.Empty;

    /// <summary>API 基址。</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>鉴权方式，见 <see cref="AuthTypes"/>。</summary>
    public string AuthType { get; init; } = AuthTypes.None;

    /// <summary><c>header</c> 鉴权下的头名（其余方式为空）。</summary>
    public string AuthHeaderName { get; init; } = string.Empty;

    /// <summary>密钥末位明文（如 <c>sk-…a1b2</c>），仅供展示；密文不出此 DTO。</summary>
    public string ApiKeyHint { get; init; } = string.Empty;

    /// <summary>私有静态配置（JSON 文本，非密，如 anthropic-version）。</summary>
    public string Settings { get; init; } = "{}";

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; }

    /// <summary>当前健康状态。</summary>
    public ProviderHealthDto Health { get; init; } = new();

    /// <summary>创建时刻（epoch ms）。</summary>
    public long CreatedAt { get; init; }

    /// <summary>最后更新时刻（epoch ms）。</summary>
    public long UpdatedAt { get; init; }
}
