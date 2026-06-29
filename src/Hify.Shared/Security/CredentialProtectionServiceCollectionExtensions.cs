using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hify.Shared.Security;

/// <summary>凭证加密的 DI 注册扩展（app 级共享）。</summary>
public static class CredentialProtectionServiceCollectionExtensions
{
    /// <summary>
    /// 注册凭证加密（<see cref="ICredentialProtector"/> + <see cref="CredentialProtectionOptions"/>）。
    /// 幂等：多个模块各自调用也只注册一次单例与一份选项，共用同一把密钥。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">配置根，用于绑定 <see cref="CredentialProtectionOptions.SectionName"/> 节。</param>
    public static IServiceCollection AddHifyCredentialProtection(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 密钥缺失不在此处校验，延迟到单例首次解析时由构造函数抛出，避免阻断无关模块的 Host 启动。
        services.AddOptions<CredentialProtectionOptions>()
            .Bind(configuration.GetSection(CredentialProtectionOptions.SectionName));
        services.TryAddSingleton<ICredentialProtector, AesCredentialProtector>();

        return services;
    }
}
