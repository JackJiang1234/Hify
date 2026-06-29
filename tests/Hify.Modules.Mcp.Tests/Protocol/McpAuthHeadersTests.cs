using Hify.Contracts.ModelProvider;
using Hify.Modules.Mcp.Protocol;

namespace Hify.Modules.Mcp.Tests.Protocol;

/// <summary>鉴权头构造：bearer / 自定义头 / 无鉴权 / 空凭证 的取值与边界。</summary>
public sealed class McpAuthHeadersTests
{
    [Fact]
    public void Build_Bearer_SetsAuthorizationHeader()
    {
        var headers = McpAuthHeaders.Build(new McpServerConnection
        {
            AuthType = AuthTypes.Bearer,
            ApiKey = "sk-abc",
        });

        Assert.NotNull(headers);
        Assert.Equal("Bearer sk-abc", headers!["Authorization"]);
    }

    [Fact]
    public void Build_Header_SetsCustomHeader()
    {
        var headers = McpAuthHeaders.Build(new McpServerConnection
        {
            AuthType = AuthTypes.Header,
            AuthHeaderName = "x-api-key",
            ApiKey = "secret",
        });

        Assert.NotNull(headers);
        Assert.Equal("secret", headers!["x-api-key"]);
    }

    [Theory]
    [InlineData(AuthTypes.None, "", "sk-abc")]      // 无鉴权：不附加
    [InlineData(AuthTypes.Bearer, "", "")]          // 凭证为空：不附加
    [InlineData(AuthTypes.Header, "", "secret")]    // header 模式但缺头名：不附加
    public void Build_NoApplicableAuth_ReturnsNull(string authType, string headerName, string apiKey)
    {
        var headers = McpAuthHeaders.Build(new McpServerConnection
        {
            AuthType = authType,
            AuthHeaderName = headerName,
            ApiKey = apiKey,
        });

        Assert.Null(headers);
    }
}
