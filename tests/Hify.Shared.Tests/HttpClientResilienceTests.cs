using System.Net;

using Hify.Shared.Resilience;

using Microsoft.Extensions.DependencyInjection;

namespace Hify.Shared.Tests;

public class HttpClientResilienceTests
{
    // 按尝试序号回放：每步为一个 HttpStatusCode 或一个待抛出的 Exception，超出长度则重复最后一步。
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly IReadOnlyList<object> _steps;
        private int _index;

        public StubHandler(params object[] steps) => _steps = steps;

        public int Attempts => _index;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var step = _steps[Math.Min(_index, _steps.Count - 1)];
            _index++;

            return step is Exception exception
                ? Task.FromException<HttpResponseMessage>(exception)
                : Task.FromResult(new HttpResponseMessage((HttpStatusCode)step));
        }
    }

    private static ResilienceOptions FastOptions() => new()
    {
        RetryCount = 3,
        RetryBaseDelayMs = 1,
        AttemptTimeoutSeconds = 30,
        MaxConcurrency = 10,
    };

    private static (HttpClient Client, StubHandler Handler) BuildClient(params object[] steps)
    {
        var handler = new StubHandler(steps);
        var services = new ServiceCollection();
        services.AddHttpClient("test")
            .AddHifyResilience(FastOptions())
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");
        return (client, handler);
    }

    [Fact]
    public async Task RetriesOnServerError_ThenSucceeds()
    {
        var (client, handler) = BuildClient(
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK);

        var response = await client.GetAsync("https://provider.test/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.Attempts);
    }

    [Fact]
    public async Task RetriesOnRateLimited()
    {
        var (client, handler) = BuildClient(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);

        var response = await client.GetAsync("https://provider.test/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.Attempts);
    }

    [Fact]
    public async Task DoesNotRetryOnUnauthorized()
    {
        var (client, handler) = BuildClient(HttpStatusCode.Unauthorized);

        var response = await client.GetAsync("https://provider.test/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task DoesNotRetryOnBadRequest()
    {
        var (client, handler) = BuildClient(HttpStatusCode.BadRequest);

        var response = await client.GetAsync("https://provider.test/");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task RetriesOnNetworkException()
    {
        var (client, handler) = BuildClient(new HttpRequestException("network glitch"), HttpStatusCode.OK);

        var response = await client.GetAsync("https://provider.test/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.Attempts);
    }

    // RetryCount=0（如 SSE 流式客户端）：管道须能正常构建并请求，且不重试。
    // 回归：此前 MaxRetryAttempts=0 会在首次请求时抛 ValidationException。
    [Fact]
    public async Task RetryCountZero_BuildsPipeline_AndDoesNotRetry()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, HttpStatusCode.OK);
        var services = new ServiceCollection();
        services.AddHttpClient("stream")
            .AddHifyResilience(new ResilienceOptions { RetryCount = 0, AttemptTimeoutSeconds = 120 })
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("stream");

        var response = await client.GetAsync("https://provider.test/");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); // 未重试，直接返回首次结果
        Assert.Equal(1, handler.Attempts);
    }
}
