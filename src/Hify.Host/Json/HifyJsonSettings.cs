using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Hify.Host.Json;

/// <summary>
/// 全局 Newtonsoft.Json 序列化策略：MVC 全局输出与异常中间件共用同一套设置。
/// camelCase 字段名；保留 null（对象不存在时返回 null，与接口规范一致）。
/// </summary>
internal static class HifyJsonSettings
{
    /// <summary>将统一策略应用到给定的 <see cref="JsonSerializerSettings"/>（用于 MVC 已有设置对象）。</summary>
    public static JsonSerializerSettings Apply(JsonSerializerSettings settings)
    {
        settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        settings.NullValueHandling = NullValueHandling.Include;
        return settings;
    }

    /// <summary>创建一份应用了统一策略的新设置（用于中间件自行序列化）。</summary>
    public static JsonSerializerSettings Create() => Apply(new JsonSerializerSettings());
}
