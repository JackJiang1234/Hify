using System.Net;

namespace Hify.Modules.ModelProvider.Adapters;

/// <summary>适配器共用：HTTP 失败状态 → 错误码映射、失败正文摘要（截断、尽力而为）。</summary>
internal static class ProviderResponse
{
    private const int FailureDetailMaxLength = 200;

    /// <summary>非成功状态映射到 2xxx 错误码。</summary>
    public static ProviderErrorCode MapStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ProviderErrorCode.ProviderAuthFailed,
        HttpStatusCode.TooManyRequests => ProviderErrorCode.ProviderRateLimited,
        _ => ProviderErrorCode.ProviderCallFailed,
    };

    /// <summary>读取失败响应正文片段拼成提示（不含本地凭证；供应商正文一般不含我方密钥）。</summary>
    public static async Task<string> DescribeFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        var status = (int)response.StatusCode;
        var detail = string.Empty;
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            detail = body.Length > FailureDetailMaxLength ? body[..FailureDetailMaxLength] : body;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
        {
            // 读取失败正文尽力而为，失败则仅返回状态码。
        }

        return detail.Length > 0 ? $"供应商返回 HTTP {status}：{detail}" : $"供应商返回 HTTP {status}";
    }
}
