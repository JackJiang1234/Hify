using System.Text.Json;

using Hify.Contracts.Mcp;
using Hify.Shared.Results;

using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Hify.Modules.Mcp.Protocol;

/// <summary>
/// 基于官方 SDK 的 Streamable HTTP 协议客户端。每次操作建立一个短暂会话（建连→握手→操作→释放）——
/// 对数据驱动、可动态增删的 Server 模型而言最简单且无状态；会话复用为后续优化项，本期不做。
/// HTTP 经命名客户端 <see cref="HttpClientName"/>（弹性管道在其上装配）。
/// </summary>
internal sealed class StreamableHttpMcpClient : IMcpProtocolClient
{
    /// <summary>命名 HttpClient（弹性管道挂载于此）。</summary>
    internal const string HttpClientName = "mcp";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public StreamableHttpMcpClient(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public Task<Result<McpServerDescriptor>> InitializeAsync(McpServerConnection connection, CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            (client, _) =>
            {
                var info = client.ServerInfo;
                return Task.FromResult(Result<McpServerDescriptor>.Ok(new McpServerDescriptor
                {
                    Name = info?.Name ?? string.Empty,
                    Version = info?.Version ?? string.Empty,
                }));
            },
            McpErrorCode.McpProtocolError,
            cancellationToken);

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<McpDiscoveredTool>>> ListToolsAsync(McpServerConnection connection, CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            async (client, ct) =>
            {
                var tools = await client.ListToolsAsync(cancellationToken: ct).ConfigureAwait(false);
                IReadOnlyList<McpDiscoveredTool> mapped = tools
                    .Select(tool => new McpDiscoveredTool
                    {
                        Name = tool.Name,
                        Description = tool.Description ?? string.Empty,
                        InputSchemaJson = tool.JsonSchema.GetRawText(),
                    })
                    .ToList();
                return Result<IReadOnlyList<McpDiscoveredTool>>.Ok(mapped);
            },
            McpErrorCode.McpProtocolError,
            cancellationToken);

    /// <inheritdoc />
    public Task<Result<McpToolResult>> CallToolAsync(
        McpServerConnection connection,
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        return ExecuteAsync(
            connection,
            async (client, ct) =>
            {
                var arguments = ParseArguments(argumentsJson);
                var result = await client.CallToolAsync(toolName, arguments, cancellationToken: ct).ConfigureAwait(false);
                return Result<McpToolResult>.Ok(new McpToolResult
                {
                    Content = FlattenContent(result),
                    IsError = result.IsError ?? false,
                });
            },
            McpErrorCode.McpToolCallFailed,
            cancellationToken);
    }

    private async Task<Result<T>> ExecuteAsync<T>(
        McpServerConnection connection,
        Func<McpClient, CancellationToken, Task<Result<T>>> operation,
        McpErrorCode protocolErrorCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        McpClient? client = null;
        try
        {
            var transport = BuildTransport(connection);
            client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken).ConfigureAwait(false);
            return await operation(client, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // 调用方主动取消，向上冒泡（区别于内部超时）。
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Fail((int)McpErrorCode.McpToolCallTimeout, "MCP 调用超时。");
        }
        catch (McpException ex)
        {
            return Result<T>.Fail((int)protocolErrorCode, $"MCP 协议错误：{ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return Result<T>.Fail((int)McpErrorCode.McpServerUnreachable, $"无法连接 MCP Server：{ex.Message}");
        }
        catch (IOException ex)
        {
            // 含 ClientTransportClosedException：传输在握手/会话期间被关闭。
            return Result<T>.Fail((int)McpErrorCode.McpServerUnreachable, $"MCP 连接中断：{ex.Message}");
        }
        finally
        {
            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private HttpClientTransport BuildTransport(McpServerConnection connection)
    {
        var options = new HttpClientTransportOptions
        {
            Endpoint = new Uri(connection.Endpoint),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = McpAuthHeaders.Build(connection),
        };

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        return new HttpClientTransport(options, httpClient, loggerFactory: null, ownsHttpClient: false);
    }

    private static IReadOnlyDictionary<string, object?>? ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson) || argumentsJson == "{}")
        {
            return null;
        }

        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson, SerializerOptions);
        if (parsed is null || parsed.Count == 0)
        {
            return null;
        }

        return parsed.ToDictionary(pair => pair.Key, pair => (object?)pair.Value);
    }

    private static string FlattenContent(CallToolResult result)
    {
        if (result.Content is null || result.Content.Count == 0)
        {
            return string.Empty;
        }

        var texts = result.Content.OfType<TextContentBlock>().Select(block => block.Text);
        return string.Join("\n", texts);
    }
}
