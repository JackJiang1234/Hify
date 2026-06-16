namespace Hify.Shared.Caching;

/// <summary>
/// 缓存键生成器。统一前缀与分段（<c>hify:{module}:{entity}[:{id}]</c>），避免键冲突、便于按段清理。
/// </summary>
public static class CacheKey
{
    /// <summary>全局键前缀。</summary>
    public const string Prefix = "hify";

    /// <summary>实体键：<c>hify:{module}:{entity}:{id}</c>。</summary>
    /// <param name="module">模块名（如 <c>provider</c>）。</param>
    /// <param name="entity">实体名（如 <c>config</c>）。</param>
    /// <param name="id">实体标识。</param>
    public static string For(string module, string entity, object id) =>
        $"{Prefix}:{module}:{entity}:{id}";

    /// <summary>集合键：<c>hify:{module}:{entity}</c>。</summary>
    /// <param name="module">模块名。</param>
    /// <param name="entity">实体名。</param>
    public static string For(string module, string entity) =>
        $"{Prefix}:{module}:{entity}";
}
