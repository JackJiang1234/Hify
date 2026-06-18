using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hify.Contracts.ModelProvider;
using Hify.Shared.Results;

namespace Hify.Modules.ModelProvider.Adapters;

/// <summary>
/// 本地 Ollama 适配器：<c>/api/chat</c>、<c>/api/embed</c>、连通性探 <c>/api/tags</c>。通常无鉴权。
/// 流式为 NDJSON（每行一个 JSON 对象，无 <c>data:</c> 前缀），与 OpenAI/Claude 的 SSE 不同。
/// </summary>
internal sealed class OllamaAdapter : IModelProviderAdapter
{
    internal const string SyncClientName = "mp-ollama";
    internal const string StreamClientName = "mp-ollama-stream";

    private const int ConnectivityTimeoutSeconds = 10;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public OllamaAdapter(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public string ProviderType => ProviderTypes.Ollama;

    /// <inheritdoc />
    public async Task<Result<ConnectionTestResult>> TestConnectionAsync(
        ProviderConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(ConnectivityTimeoutSeconds));

        var client = _httpClientFactory.CreateClient(SyncClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, ProviderHttp.Combine(connection.BaseUrl, "api/tags"));
        ProviderHttp.ApplyAuth(request, connection);

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
            return Result<ConnectionTestResult>.Fail((int)ProviderErrorCode.ProviderUnreachable, $"无法连接 Ollama：{ex.Message}");
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
        using var httpRequest = CreatePostRequest(connection, "api/chat", BuildChatPayload(model, request, stream: false));

        try
        {
            using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<ChatResponse>.Fail((int)ProviderResponse.MapStatus(response.StatusCode), await ProviderResponse.DescribeFailureAsync(response, cancellationToken));
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<OllamaChatResponse>(body, SerializerOptions);
            if (parsed?.Message is null)
            {
                return Result<ChatResponse>.Fail((int)ProviderErrorCode.ProviderResponseInvalid, "Ollama 响应缺少 message。");
            }

            return Result<ChatResponse>.Ok(new ChatResponse
            {
                Content = parsed.Message.Content ?? string.Empty,
                FinishReason = parsed.DoneReason ?? string.Empty,
                PromptTokens = parsed.PromptEvalCount,
                CompletionTokens = parsed.EvalCount,
            });
        }
        catch (HttpRequestException ex)
        {
            return Result<ChatResponse>.Fail((int)ProviderErrorCode.ProviderUnreachable, $"调用 Ollama 失败：{ex.Message}");
        }
        catch (JsonException)
        {
            return Result<ChatResponse>.Fail((int)ProviderErrorCode.ProviderResponseInvalid, "Ollama 响应解析失败。");
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
        var httpRequest = CreatePostRequest(connection, "api/chat", BuildChatPayload(model, request, stream: true));

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            httpRequest.Dispose();
            return Result<IAsyncEnumerable<ChatStreamChunk>>.Fail((int)ProviderErrorCode.ProviderUnreachable, $"调用 Ollama 失败：{ex.Message}");
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
        using var httpRequest = CreatePostRequest(connection, "api/embed", new OllamaEmbeddingRequest(model, request.Inputs));

        try
        {
            using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<EmbeddingResponse>.Fail((int)ProviderResponse.MapStatus(response.StatusCode), await ProviderResponse.DescribeFailureAsync(response, cancellationToken));
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(body, SerializerOptions);
            if (parsed?.Embeddings is null)
            {
                return Result<EmbeddingResponse>.Fail((int)ProviderErrorCode.ProviderResponseInvalid, "Ollama 响应缺少 embeddings。");
            }

            var vectors = parsed.Embeddings
                .Select(vector => (IReadOnlyList<float>)(vector ?? []))
                .ToList();

            return Result<EmbeddingResponse>.Ok(new EmbeddingResponse
            {
                Vectors = vectors,
                PromptTokens = parsed.PromptEvalCount,
            });
        }
        catch (HttpRequestException ex)
        {
            return Result<EmbeddingResponse>.Fail((int)ProviderErrorCode.ProviderUnreachable, $"调用 Ollama 失败：{ex.Message}");
        }
        catch (JsonException)
        {
            return Result<EmbeddingResponse>.Fail((int)ProviderErrorCode.ProviderResponseInvalid, "Ollama 响应解析失败。");
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
                if (line.Length == 0)
                {
                    continue;
                }

                OllamaChatResponse? chunk = null;
                try
                {
                    chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, SerializerOptions);
                }
                catch (JsonException)
                {
                    // 跳过无法解析的行。
                }

                if (chunk is null)
                {
                    continue;
                }

                var delta = chunk.Message?.Content;
                if (!string.IsNullOrEmpty(delta))
                {
                    yield return new ChatStreamChunk { Delta = delta };
                }

                if (chunk.Done)
                {
                    finishReason = chunk.DoneReason ?? string.Empty;
                    promptTokens = chunk.PromptEvalCount;
                    completionTokens = chunk.EvalCount;
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
        return request;
    }

    private static OllamaChatRequest BuildChatPayload(string model, ChatRequest request, bool stream) =>
        new(
            model,
            request.Messages.Select(message => new OllamaMessage(message.Role, message.Content)).ToList(),
            stream,
            new OllamaOptions(request.MaxTokens, request.Temperature, request.TopP));

    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OllamaMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options")] OllamaOptions Options);

    private sealed record OllamaOptions(
        [property: JsonPropertyName("num_predict")] int NumPredict,
        [property: JsonPropertyName("temperature")] double? Temperature,
        [property: JsonPropertyName("top_p")] double? TopP);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content);

    private sealed record OllamaEmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] IReadOnlyList<string> Input);

    private sealed record OllamaChatResponse
    {
        [JsonPropertyName("message")] public OllamaMessage? Message { get; init; }

        [JsonPropertyName("done")] public bool Done { get; init; }

        [JsonPropertyName("done_reason")] public string? DoneReason { get; init; }

        [JsonPropertyName("prompt_eval_count")] public long PromptEvalCount { get; init; }

        [JsonPropertyName("eval_count")] public long EvalCount { get; init; }
    }

    private sealed record OllamaEmbeddingResponse
    {
        [JsonPropertyName("embeddings")] public IReadOnlyList<float[]?>? Embeddings { get; init; }

        [JsonPropertyName("prompt_eval_count")] public long PromptEvalCount { get; init; }
    }
}
