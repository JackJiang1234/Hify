namespace Hify.Modules.ModelProvider.Adapters;

/// <summary>按 <see cref="IModelProviderAdapter.ProviderType"/> 建索引，O(1) 选择适配器。</summary>
internal sealed class ModelProviderAdapterFactory : IModelProviderAdapterFactory
{
    private readonly IReadOnlyDictionary<string, IModelProviderAdapter> _adapters;

    /// <summary>构造，按类型索引所有已注册适配器。</summary>
    /// <param name="adapters">DI 注入的全部适配器。</param>
    public ModelProviderAdapterFactory(IEnumerable<IModelProviderAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        _adapters = adapters.ToDictionary(adapter => adapter.ProviderType, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IModelProviderAdapter Get(string providerType) =>
        _adapters.TryGetValue(providerType, out var adapter)
            ? adapter
            : throw new NotSupportedException($"未知或未启用的供应商类型：{providerType}");
}
