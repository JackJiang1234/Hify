using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hify.Shared.Modularity;

/// <summary>
/// 模块注册契约：每个业务模块通过唯一的 *Module 入口实现本接口，由 Host（组合根）统一装配。
/// 模块间不直接互相引用，仅通过 Hify.Contracts 协作。
/// </summary>
public interface IModule
{
    /// <summary>将模块自身的服务注册到 DI 容器。</summary>
    /// <param name="services">DI 服务集合。</param>
    /// <param name="configuration">应用配置（连接串、外部服务地址等）。</param>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
}
