using System.ComponentModel;

using Hify.Contracts.ModelProvider;
using Hify.Modules.Mcp.Protocol;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using ModelContextProtocol.Server;

namespace Hify.Modules.Mcp.Tests.Protocol;

/// <summary>
/// 协议客户端集成测试：用真实 in-process MCP server（AspNetCore + TestHost）走真 HTTP，
/// 验证发现 / 调用 / 工具级错误 / 不可达 的真实往返。
/// </summary>
public sealed class StreamableHttpMcpClientTests : IAsyncLifetime
{
    private IHost _host = null!;
    private StreamableHttpMcpClient _client = null!;
    private McpServerConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddMcpServer().WithHttpTransport().WithTools<EchoTools>();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapMcp());
                });
            })
            .StartAsync();

        var testServer = _host.GetTestServer();
        _client = new StreamableHttpMcpClient(new SingleClientFactory(testServer.CreateClient()));
        _connection = new McpServerConnection { Endpoint = testServer.BaseAddress.ToString() };
    }

    public async Task DisposeAsync() => await _host.StopAsync();

    [Fact]
    public async Task InitializeAsync_Handshake_Succeeds()
    {
        var result = await _client.InitializeAsync(_connection, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task ListToolsAsync_ReturnsDiscoveredTools()
    {
        var result = await _client.ListToolsAsync(_connection, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.NotNull(result.Data);
        Assert.Contains(result.Data!, tool => tool.Name == "echo");
        var echo = result.Data!.First(tool => tool.Name == "echo");
        Assert.False(string.IsNullOrEmpty(echo.InputSchemaJson));
    }

    [Fact]
    public async Task CallToolAsync_Echo_FlattensTextContent()
    {
        var result = await _client.CallToolAsync(
            _connection, "echo", """{"message":"hello mcp"}""", CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.NotNull(result.Data);
        Assert.Contains("hello mcp", result.Data!.Content, StringComparison.Ordinal);
        Assert.False(result.Data.IsError);
    }

    [Fact]
    public async Task CallToolAsync_ToolThrows_ReturnsIsError()
    {
        var result = await _client.CallToolAsync(
            _connection, "fail", "{}", CancellationToken.None);

        // 工具级错误：调用本身成功（200），但 IsError=true。
        Assert.Equal(200, result.Code);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.IsError);
    }

    [Fact]
    public async Task CallToolAsync_UnreachableEndpoint_ReturnsUnreachable()
    {
        // 指向一个本机关闭端口，用真实 HttpClient（非 TestServer）触发连接失败。
        var deadClient = new StreamableHttpMcpClient(new SingleClientFactory(new HttpClient()));
        var deadConnection = new McpServerConnection { Endpoint = "http://localhost:1/" };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var result = await deadClient.CallToolAsync(deadConnection, "echo", "{}", cts.Token);

        Assert.Equal((int)McpErrorCode.McpServerUnreachable, result.Code);
    }

    /// <summary>把单个 HttpClient 当作工厂返回，便于把 TestServer 的客户端喂给协议客户端。</summary>
    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SingleClientFactory(HttpClient client) => _client = client;

        public HttpClient CreateClient(string name) => _client;
    }

    /// <summary>测试用 MCP 工具集：echo 回显文本，fail 抛错以驱动 isError 路径。</summary>
    [McpServerToolType]
    internal sealed class EchoTools
    {
        [McpServerTool(Name = "echo")]
        [Description("回显传入的 message。")]
        public static string Echo(string message) => message;

        [McpServerTool(Name = "fail")]
        [Description("总是抛错，用于测试工具级错误。")]
        public static string Fail() => throw new InvalidOperationException("boom");
    }
}
