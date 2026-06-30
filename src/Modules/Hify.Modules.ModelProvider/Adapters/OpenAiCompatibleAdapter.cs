using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;

using Hify.Contracts.ModelProvider;
using Hify.Shared.Results;

namespace Hify.Modules.ModelProvider.Adapters;

/// <summary>
/// OpenAI 及兼容厂商（vLLM/LM Studio/多数国内厂商）的适配器：裸 HttpClient + System.Text.Json。
/// 同步调用走 <see cref="SyncClientName"/> 客户端，流式走 <see cref="StreamClientName"/>（更长超时、不重试）。
/// </summary>
internal sealed class OpenAiCompatibleAdapter : IModelProviderAdapter
{
    internal const string SyncClientName = "mp-openai";
    internal const string StreamClientName = "mp-openai-stream";

    private const int ConnectivityTimeoutSeconds = 10;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public OpenAiCompatibleAdapter(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public string ProviderType => ProviderTypes.OpenAi;

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

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Result<ConnectionTestResult>.Fail(
                    (int)ProviderResponse.MapStatus(response.StatusCode),
                    $"连通性测试失败：HTTP {(int)response.StatusCode}");
            }

            var latencyMs = (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
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
        using var httpRequest = CreatePostRequest(connection, "chat/completions", BuildChatPayload(model, request, stream: false));

        try
        {
            using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<ChatResponse>.Fail((int)ProviderResponse.MapStatus(response.StatusCode), await ProviderResponse.DescribeFailureAsync(response, cancellationToken));
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<OpenAiChatResponse>(body, SerializerOptions);
            if (parsed?.Choices is null || parsed.Choices.Count == 0)
            {
                return Result<ChatResponse>.Fail((int)ProviderErrorCode.ProviderResponseInvalid, "供应商响应缺少 choices。");
            }

            var choice = parsed.Choices[0];
            return Result<ChatResponse>.Ok(new ChatResponse
            {
                Content = choice.Message?.Content ?? string.Empty,
                FinishReason = choice.FinishReason ?? string.Empty,
                ToolCalls = MapToolCalls(choice.Message?.ToolCalls),
                PromptTokens = parsed.Usage?.PromptTokens ?? 0,
                CompletionTokens = parsed.Usage?.CompletionTokens ?? 0,
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
        var httpRequest = CreatePostRequest(connection, "chat/completions", BuildChatPayload(model, request, stream: true));

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
    public async Task<Result<EmbeddingResponse>> EmbedAsync(
        ProviderConnection connection,
        string model,
        EmbeddingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);

        var client = _httpClientFactory.CreateClient(SyncClientName);
        using var httpRequest = CreatePostRequest(connection, "embeddings", new OpenAiEmbeddingRequest(model, request.Inputs));

        try
        {
            using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<EmbeddingResponse>.Fail((int)ProviderResponse.MapStatus(response.StatusCode), await ProviderResponse.DescribeFailureAsync(response, cancellationToken));
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<OpenAiEmbeddingResponse>(body, SerializerOptions);
            if (parsed?.Data is null)
            {
                return Result<EmbeddingResponse>.Fail((int)ProviderErrorCode.ProviderResponseInvalid, "供应商响应缺少 data。");
            }

            var vectors = parsed.Data
                .Select(item => (IReadOnlyList<float>)(item.Embedding ?? []))
                .ToList();

            return Result<EmbeddingResponse>.Ok(new EmbeddingResponse
            {
                Vectors = vectors,
                PromptTokens = parsed.Usage?.PromptTokens ?? 0,
            });
        }
        catch (HttpRequestException ex)
        {
            return Result<EmbeddingResponse>.Fail((int)ProviderErrorCode.ProviderUnreachable, $"调用供应商失败：{ex.Message}");
        }
        catch (JsonException)
        {
            return Result<EmbeddingResponse>.Fail((int)ProviderErrorCode.ProviderResponseInvalid, "供应商响应解析失败。");
        }
    }

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
                    continue;
                }

                var data = line["data:".Length..].Trim();
                if (data.Length == 0)
                {
                    continue;
                }

                if (data == "[DONE]")
                {
                    break;
                }

                OpenAiStreamChunk? chunk = null;
                try
                {
                    chunk = JsonSerializer.Deserialize<OpenAiStreamChunk>(data, SerializerOptions);
                }
                catch (JsonException)
                {
                    // 跳过无法解析的片段，不中断整条流。
                }

                if (chunk is null)
                {
                    continue;
                }

                if (chunk.Usage is not null)
                {
                    promptTokens = chunk.Usage.PromptTokens;
                    completionTokens = chunk.Usage.CompletionTokens;
                }

                if (chunk.Choices is { Count: > 0 })
                {
                    var choice = chunk.Choices[0];
                    if (!string.IsNullOrEmpty(choice.FinishReason))
                    {
                        finishReason = choice.FinishReason;
                    }

                    var delta = choice.Delta?.Content;
                    if (!string.IsNullOrEmpty(delta))
                    {
                        yield return new ChatStreamChunk { Delta = delta };
                    }
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
        return request;
    }

    private static OpenAiChatRequest BuildChatPayload(string model, ChatRequest request, bool stream) =>
        new(
            model,
            request.Messages.Select(ToOpenAiMessage).ToList(),
            request.MaxTokens,
            request.Temperature,
            request.TopP,
            stream,
            stream ? new OpenAiStreamOptions(true) : null,
            BuildTools(request.Tools));

    private static OpenAiMessage ToOpenAiMessage(ChatMessage message)
    {
        if (message.ToolCalls.Count > 0)
        {
            var toolCalls = message.ToolCalls
                .Select(call => new OpenAiToolCall(call.Id, "function", new OpenAiFunctionCall(call.Name, call.ArgumentsJson)))
                .ToList();
            // assistant 发起工具调用：content 可空（null 触发 WhenWritingNull 省略）。
            return new OpenAiMessage(message.Role, string.IsNullOrEmpty(message.Content) ? null : message.Content, toolCalls);
        }

        if (!string.IsNullOrEmpty(message.ToolCallId))
        {
            return new OpenAiMessage(message.Role, message.Content, ToolCallId: message.ToolCallId);
        }

        return new OpenAiMessage(message.Role, message.Content);
    }

    private static IReadOnlyList<OpenAiTool>? BuildTools(IReadOnlyList<ToolDefinition> tools)
    {
        if (tools.Count == 0)
        {
            return null;
        }

        return tools
            .Select(tool => new OpenAiTool("function", new OpenAiFunction(tool.Name, tool.Description, ParseJsonObject(tool.ParametersJson))))
            .ToList();
    }

    private static JsonElement ParseJsonObject(string json)
    {
        var text = string.IsNullOrWhiteSpace(json) ? "{}" : json;
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone(); // Clone 脱离 document 生命周期，序列化时仍可用
    }

    private static IReadOnlyList<ToolCall> MapToolCalls(IReadOnlyList<OpenAiResponseToolCall>? toolCalls)
    {
        if (toolCalls is null || toolCalls.Count == 0)
        {
            return [];
        }

        return toolCalls
            .Select(call => new ToolCall
            {
                Id = call.Id ?? string.Empty,
                Name = call.Function?.Name ?? string.Empty,
                ArgumentsJson = string.IsNullOrEmpty(call.Function?.Arguments) ? "{}" : call.Function!.Arguments!,
            })
            .ToList();
    }

    private sealed record OpenAiChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiMessage> Messages,
        // 新版 OpenAI 模型不再接受 max_tokens，须用 max_completion_tokens（旧字段在新模型上会被拒）。
        [property: JsonPropertyName("max_completion_tokens")] int MaxTokens,
        [property: JsonPropertyName("temperature")] double? Temperature,
        [property: JsonPropertyName("top_p")] double? TopP,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("stream_options")] OpenAiStreamOptions? StreamOptions,
        [property: JsonPropertyName("tools")] IReadOnlyList<OpenAiTool>? Tools);

    private sealed record OpenAiStreamOptions([property: JsonPropertyName("include_usage")] bool IncludeUsage);

    private sealed record OpenAiMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("tool_calls")] IReadOnlyList<OpenAiToolCall>? ToolCalls = null,
        [property: JsonPropertyName("tool_call_id")] string? ToolCallId = null);

    private sealed record OpenAiTool(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("function")] OpenAiFunction Function);

    private sealed record OpenAiFunction(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("parameters")] JsonElement Parameters);

    private sealed record OpenAiToolCall(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("function")] OpenAiFunctionCall Function);

    private sealed record OpenAiFunctionCall(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("arguments")] string Arguments);

    private sealed record OpenAiEmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] IReadOnlyList<string> Input);

    private sealed record OpenAiChatResponse
    {
        [JsonPropertyName("choices")] public IReadOnlyList<OpenAiChoice>? Choices { get; init; }

        [JsonPropertyName("usage")] public OpenAiUsage? Usage { get; init; }
    }

    private sealed record OpenAiStreamChunk
    {
        [JsonPropertyName("choices")] public IReadOnlyList<OpenAiChoice>? Choices { get; init; }

        [JsonPropertyName("usage")] public OpenAiUsage? Usage { get; init; }
    }

    private sealed record OpenAiChoice
    {
        [JsonPropertyName("message")] public OpenAiMessageContent? Message { get; init; }

        [JsonPropertyName("delta")] public OpenAiMessageContent? Delta { get; init; }

        [JsonPropertyName("finish_reason")] public string? FinishReason { get; init; }
    }

    private sealed record OpenAiMessageContent
    {
        [JsonPropertyName("content")] public string? Content { get; init; }

        [JsonPropertyName("tool_calls")] public IReadOnlyList<OpenAiResponseToolCall>? ToolCalls { get; init; }
    }

    private sealed record OpenAiResponseToolCall
    {
        [JsonPropertyName("id")] public string? Id { get; init; }

        [JsonPropertyName("function")] public OpenAiResponseFunction? Function { get; init; }
    }

    private sealed record OpenAiResponseFunction
    {
        [JsonPropertyName("name")] public string? Name { get; init; }

        [JsonPropertyName("arguments")] public string? Arguments { get; init; }
    }

    private sealed record OpenAiUsage
    {
        [JsonPropertyName("prompt_tokens")] public long PromptTokens { get; init; }

        [JsonPropertyName("completion_tokens")] public long CompletionTokens { get; init; }
    }

    private sealed record OpenAiEmbeddingResponse
    {
        [JsonPropertyName("data")] public IReadOnlyList<OpenAiEmbeddingData>? Data { get; init; }

        [JsonPropertyName("usage")] public OpenAiUsage? Usage { get; init; }
    }

    private sealed record OpenAiEmbeddingData
    {
        [JsonPropertyName("embedding")] public float[]? Embedding { get; init; }
    }
}
