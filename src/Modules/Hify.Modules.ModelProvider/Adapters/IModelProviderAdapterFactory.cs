namespace Hify.Modules.ModelProvider.Adapters;

/// <summary>按供应商类型选择适配器。</summary>
internal interface IModelProviderAdapterFactory
{
    /// <summary>取指定类型的适配器；未知/未启用类型抛 <see cref="NotSupportedException"/>。</summary>
    /// <param name="providerType">供应商类型，见 <see cref="Hify.Contracts.ModelProvider.ProviderTypes"/>。</param>
    IModelProviderAdapter Get(string providerType);
}
