using System.Text.Json;
using System.Text.Json.Serialization;

using Hify.Contracts.Agent;

namespace Hify.Modules.Agent.Features.Agents;

/// <summary>
/// Agent 参数与 jsonb 文本之间的强类型编解码。落库前请求已校验，故反序列化的是可信文本；
/// 容错处理空串/"{}"，避免历史脏数据导致映射抛错。
/// </summary>
internal static class AgentParamsJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>序列化生成参数；<c>null</c> 视为不覆盖任何参数，存空对象。</summary>
    public static string SerializeModelParams(ModelParams? value) =>
        JsonSerializer.Serialize(value ?? new ModelParams(), Options);

    /// <summary>序列化检索参数。</summary>
    public static string SerializeRetrievalParams(RetrievalParams value) =>
        JsonSerializer.Serialize(value, Options);

    /// <summary>反序列化生成参数；空串/空对象得到全空字段的实例。</summary>
    public static ModelParams DeserializeModelParams(string json) =>
        Deserialize(json, () => new ModelParams());

    /// <summary>反序列化检索参数；空串得到默认实例（TopK=3）。</summary>
    public static RetrievalParams DeserializeRetrievalParams(string json) =>
        Deserialize(json, () => new RetrievalParams());

    private static T Deserialize<T>(string json, Func<T> fallback)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback();
        }

        return JsonSerializer.Deserialize<T>(json, Options) ?? fallback();
    }
}
