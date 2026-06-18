using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Domain;

namespace Hify.Modules.ModelProvider.Features.Providers;

/// <summary>实体 → 脱敏 DTO 映射。密钥只出 <see cref="ProviderDto.ApiKeyHint"/>。</summary>
internal static class ProviderMapping
{
    public static ProviderDto ToDto(Provider provider, ProviderHealth? health) => new()
    {
        Id = provider.Id,
        Name = provider.Name,
        ProviderType = provider.ProviderType,
        BaseUrl = provider.BaseUrl,
        AuthType = provider.AuthType,
        AuthHeaderName = provider.AuthHeaderName,
        ApiKeyHint = provider.ApiKeyHint,
        Settings = provider.Settings,
        Enabled = provider.Enabled,
        Health = health is null ? new ProviderHealthDto() : ToHealthDto(health),
        CreatedAt = provider.CreatedAt,
        UpdatedAt = provider.UpdatedAt,
    };

    public static ProviderHealthDto ToHealthDto(ProviderHealth health) => new()
    {
        Status = health.Status,
        LatencyMs = health.LatencyMs,
        ConsecutiveFailures = health.ConsecutiveFailures,
        LastError = health.LastError,
        CheckedAt = health.CheckedAt,
    };
}
