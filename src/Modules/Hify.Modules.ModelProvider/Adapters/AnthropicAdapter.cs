using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hify.Contracts.ModelProvider;
using Hify.Shared.Results;

namespace Hify.Modules.ModelProvider.Adapters;

/// <summary>
/// Anthropic Claude 适配器（<c>/v1/messages</c>）。鉴权用 <c>x-api-key</c>（来自连接），并确保带 <c>anthropic-version</c>。
/// system 提示走顶层 <c>system</c> 字段；多事件 SSE 流式；不提供嵌入。
/// </summary>
internal sealed class AnthropicAdapter : IModelProviderAdapter
{
    internal const string SyncClientName = "mp-claude";
    internal const string StreamClientName = "mp-claude-stream";

    private const string AnthropicVersionHeader = "anthropic-version";
    private const string DefaultAnthropicVersion = "2023-06-01";
    private const int ConnectivityTimeoutSeconds = 10;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public AnthropicAdapter(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public string ProviderType => ProviderTypes.Claude;

    /// <inheritdoc />
    public async Task<Result<ConnectionTestResult>> TestConnectionAsync(
        ProviderConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(ConnectivityTimeoutSeconds));

        var client = _httpClientFactory.CreateClient(SyncClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, ProviderHttp.Combine(connection.BaseUrl, "models"));
        ProviderHttp.ApplyAuth(request, connection);
        EnsureAnthropicVersion(request);

        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Result<ConnectionTestResult>.Fail(
                    (int)ProviderResponse.MapStatus(response.StatusCode),
                    $"连通性测试失败：HTTP {(int)response.StatusCode}");
            }

            var latencyMs = (int)System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            return Result<ConnectionTestResult>.Ok(new ConnectionTestResult { LatencyMs = latencyMs });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<ConnectionTestResult>.Fail((int)ProviderErrorCode.ProviderUnreachable, "连通性测试超时。");
        }
        catch (HttpRequestException ex)
        {
            return Result<ConnectionTestResult>.Fail((int)ProviderErrorCode.ProviderUnreachable, $"无法连接供应商：{ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<ChatResponse>> ChatAsync(
        ProviderConnection connection,
        string model,
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);

        var client = _httpClientFactory.CreateClient(SyncClientName);
        using var httpRequest = CreatePostRequest(connection, "messages", BuildChatPayload(model, request, stream: false));

        try
        {
            using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<ChatResponse>.Fail((int)ProviderResponse.MapStatus(response.StatusCode), await ProviderResponse.DescribeFailureAsync(response, cancellationToken));
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<AnthropicChatResponse>(body, SerializerOptions);
            if (parsed?.Content is null)
            {
                return Result<ChatResponse>.Fail((int)ProviderErrorCode.ProviderResponseInvalid, "供应商响应缺少 content。");
            }

            var text = string.Concat(parsed.Content.Where(block => block.Type == "text").Select(block => block.Text));
            return Result<ChatResponse>.Ok(new ChatResponse
            {
                Content = text,
                FinishReason = parsed.StopReason ?? string.Empty,
                PromptTokens = parsed.Usage?.InputTokens ?? 0,
                CompletionTokens = parsed.Usage?.OutputTokens ?? 0,
            });
        }
        catch (HttpRequestException ex)
        {
            return Result<ChatResponse>.Fail((int)ProviderErrorCode.ProviderUnreachable, $"调用供应商失败：{ex.Message}");
        }
        catch (JsonException)
        {
            return Result<ChatResponse>.Fail((int)ProviderErrorCode.ProviderResponseInvalid, "供应商响应解析失败。");
        }
    }

    /// <inheritdoc />
    public async Task<Result<IAsyncEnumerable<ChatStreamChunk>>> ChatStreamAsync(
        ProviderConnection connection,
        string model,
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);

        var client = _httpClientFactory.CreateClient(StreamClientName);
        var httpRequest = CreatePostRequest(connection, "messages", BuildChatPayload(model, request, stream: true));

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            httpRequest.Dispose();
            return Result<IAsyncEnumerable<ChatStreamChunk>>.Fail((int)ProviderErrorCode.ProviderUnreachable, $"调用供应商失败：{ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = await ProviderResponse.DescribeFailureAsync(response, cancellationToken);
            var code = ProviderResponse.MapStatus(response.StatusCode);
            response.Dispose();
            httpRequest.Dispose();
            return Result<IAsyncEnumerable<ChatStreamChunk>>.Fail((int)code, message);
        }

        return Result<IAsyncEnumerable<ChatStreamChunk>>.Ok(StreamChunksAsync(response, httpRequest, cancellationToken));
    }

    /// <inheritdoc />
    public Task<Result<EmbeddingResponse>> EmbedAsync(
        ProviderConnection connection,
        string model,
        EmbeddingRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result<EmbeddingResponse>.Fail(
            (int)ProviderErrorCode.EmbeddingNotSupported,
            "Claude 不提供嵌入接口，请改用 OpenAI / Ollama 的嵌入模型。"));

    private static async IAsyncEnumerable<ChatStreamChunk> StreamChunksAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var finishReason = string.Empty;
            long promptTokens = 0;
            long completionTokens = 0;

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!line.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue; // 忽略 event: 等其它行
                }

                var data = line["data:".Length..].Trim();
                if (data.Length == 0)
                {
                    continue;
                }

                AnthropicStreamEvent? evt = null;
                try
                {
                    evt = JsonSerializer.Deserialize<AnthropicStreamEvent>(data, SerializerOptions);
                }
                catch (JsonException)
                {
                    // 跳过无法解析的片段。
                }

                if (evt is null)
                {
                    continue;
                }

                switch (evt.Type)
                {
                    case "message_start":
                        promptTokens = evt.Message?.Usage?.InputTokens ?? promptTokens;
                        break;

                    case "content_block_delta":
                        var delta = evt.Delta?.Text;
                        if (!string.IsNullOrEmpty(delta))
                        {
                            yield return new ChatStreamChunk { Delta = delta };
                        }

                        break;

                    case "message_delta":
                        if (!string.IsNullOrEmpty(evt.Delta?.StopReason))
                        {
                            finishReason = evt.Delta!.StopReason!;
                        }

                        completionTokens = evt.Usage?.OutputTokens ?? completionTokens;
                        break;

                    case "message_stop":
                        yield return new ChatStreamChunk
                        {
                            IsFinal = true,
                            FinishReason = finishReason,
                            PromptTokens = promptTokens,
                            CompletionTokens = completionTokens,
                        };
                        yield break;

                    default:
                        break;
                }
            }

            yield return new ChatStreamChunk
            {
                IsFinal = true,
                FinishReason = finishReason,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
            };
        }
        finally
        {
            response.Dispose();
            request.Dispose();
        }
    }

    private static HttpRequestMessage CreatePostRequest<TPayload>(ProviderConnection connection, string path, TPayload payload)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, ProviderHttp.Combine(connection.BaseUrl, path))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        ProviderHttp.ApplyAuth(request, connection);
        EnsureAnthropicVersion(request);
        return request;
    }

    private static void EnsureAnthropicVersion(HttpRequestMessage request)
    {
        if (!request.Headers.Contains(AnthropicVersionHeader))
        {
            request.Headers.TryAddWithoutValidation(AnthropicVersionHeader, DefaultAnthropicVersion);
        }
    }

    private static AnthropicChatRequest BuildChatPayload(string model, ChatRequest request, bool stream)
    {
        var systemText = string.Join(
            "\n",
            request.Messages.Where(message => message.Role == "system").Select(message => message.Content));

        var messages = request.Messages
            .Where(message => message.Role != "system")
            .Select(message => new AnthropicMessage(message.Role, message.Content))
            .ToList();

        return new AnthropicChatRequest(
            model,
            request.MaxTokens,
            string.IsNullOrEmpty(systemText) ? null : systemText,
            messages,
            request.Temperature,
            request.TopP,
            stream);
    }

    private sealed record AnthropicChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string? System,
        [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages,
        [property: JsonPropertyName("temperature")] double? Temperature,
        [property: JsonPropertyName("top_p")] double? TopP,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record AnthropicMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record AnthropicChatResponse
    {
        [JsonPropertyName("content")] public IReadOnlyList<AnthropicContentBlock>? Content { get; init; }

        [JsonPropertyName("stop_reason")] public string? StopReason { get; init; }

        [JsonPropertyName("usage")] public AnthropicUsage? Usage { get; init; }
    }

    private sealed record AnthropicContentBlock
    {
        [JsonPropertyName("type")] public string? Type { get; init; }

        [JsonPropertyName("text")] public string? Text { get; init; }
    }

    private sealed record AnthropicStreamEvent
    {
        [JsonPropertyName("type")] public string? Type { get; init; }

        [JsonPropertyName("delta")] public AnthropicStreamDelta? Delta { get; init; }

        [JsonPropertyName("usage")] public AnthropicUsage? Usage { get; init; }

        [JsonPropertyName("message")] public AnthropicStreamMessage? Message { get; init; }
    }

    private sealed record AnthropicStreamDelta
    {
        [JsonPropertyName("text")] public string? Text { get; init; }

        [JsonPropertyName("stop_reason")] public string? StopReason { get; init; }
    }

    private sealed record AnthropicStreamMessage
    {
        [JsonPropertyName("usage")] public AnthropicUsage? Usage { get; init; }
    }

    private sealed record AnthropicUsage
    {
        [JsonPropertyName("input_tokens")] public long InputTokens { get; init; }

        [JsonPropertyName("output_tokens")] public long OutputTokens { get; init; }
    }
}
