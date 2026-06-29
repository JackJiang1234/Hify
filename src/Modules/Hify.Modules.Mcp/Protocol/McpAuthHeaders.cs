using Hify.Contracts.ModelProvider;

namespace Hify.Modules.Mcp.Protocol;

/// <summary>把连接的鉴权配置转换为发往 MCP Server 的 HTTP 头（纯函数，便于单测）。</summary>
internal static class McpAuthHeaders
{
    /// <summary>
    /// 构造附加请求头：<c>bearer</c> → <c>Authorization: Bearer &lt;key&gt;</c>；
    /// <c>header</c> → 自定义头名注入凭证；其余或凭证为空返回 <see langword="null"/>（不附加头）。
    /// </summary>
    /// <param name="connection">连接信息。</param>
    public static IDictionary<string, string>? Build(McpServerConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrEmpty(connection.ApiKey))
        {
            return null;
        }

        return connection.AuthType switch
        {
            AuthTypes.Bearer => new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {connection.ApiKey}",
            },
            AuthTypes.Header when !string.IsNullOrEmpty(connection.AuthHeaderName) => new Dictionary<string, string>
            {
                [connection.AuthHeaderName] = connection.ApiKey,
            },
            _ => null,
        };
    }
}
