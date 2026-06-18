using System.Net.Http.Headers;
using System.Text.Json;

using Hify.Contracts.ModelProvider;

namespace Hify.Modules.ModelProvider.Adapters;

/// <summary>适配器共用：按鉴权方式注入密钥、把 settings 作为静态请求头追加、拼接 URL。</summary>
internal static class ProviderHttp
{
    /// <summary>
    /// 按 <see cref="ProviderConnection.AuthType"/> 注入密钥；并将 settings（JSON「头名→值」映射，
    /// 如 <c>{"anthropic-version":"2023-06-01"}</c>）作为静态请求头追加。
    /// </summary>
    public static void ApplyAuth(HttpRequestMessage request, ProviderConnection connection)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(connection);

        switch (connection.AuthType)
        {
            case AuthTypes.Bearer when connection.ApiKey.Length > 0:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);
                break;
            case AuthTypes.Header when connection.AuthHeaderName.Length > 0 && connection.ApiKey.Length > 0:
                request.Headers.TryAddWithoutValidation(connection.AuthHeaderName, connection.ApiKey);
                break;
            default:
                break;
        }

        ApplyStaticHeaders(request, connection.Settings);
    }

    /// <summary>拼接 base URL 与子路径，规整中间斜杠。</summary>
    public static string Combine(string baseUrl, string path) =>
        $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

    private static void ApplyStaticHeaders(HttpRequestMessage request, string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson == "{}")
        {
            return;
        }

        Dictionary<string, string>? headers;
        try
        {
            headers = JsonSerializer.Deserialize<Dictionary<string, string>>(settingsJson);
        }
        catch (JsonException)
        {
            return; // settings 非「头名→值」映射则忽略，不阻断请求。
        }

        if (headers is null)
        {
            return;
        }

        foreach (var (name, value) in headers)
        {
            if (!string.IsNullOrEmpty(name))
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }
    }
}
