using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Adapters;
using Hify.Modules.ModelProvider.Domain;
using Hify.Modules.ModelProvider.Features.Providers;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Modules.ModelProvider.Security;
using Hify.Modules.ModelProvider.Tests.Support;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.ModelProvider.Tests.Features.Providers;

/// <summary>连通性测试服务的真实库测试（连不上则跳过）：成功记 healthy、探活/解密失败记 unhealthy。</summary>
public sealed class ProviderConnectivityServiceTests : IAsyncLifetime
{
    private bool _available;

    public async Task InitializeAsync() => _available = await TestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class StubAdapter(Result<ConnectionTestResult> probeResult) : IModelProviderAdapter
    {
        public string ProviderType => ProviderTypes.OpenAi;

        public Task<Result<ConnectionTestResult>> TestConnectionAsync(ProviderConnection connection, CancellationToken cancellationToken) =>
            Task.FromResult(probeResult);

        public Task<Result<ChatResponse>> ChatAsync(ProviderConnection connection, string model, ChatRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<IAsyncEnumerable<ChatStreamChunk>>> ChatStreamAsync(ProviderConnection connection, string model, ChatRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<EmbeddingResponse>> EmbedAsync(ProviderConnection connection, string model, EmbeddingRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubFactory(IModelProviderAdapter adapter) : IModelProviderAdapterFactory
    {
        public IModelProviderAdapter Get(string providerType) => adapter;
    }

    private static async Task<long> SeedAsync(ModelProviderDbContext db, ICredentialProtector protector, string? cipherOverride = null)
    {
        var provider = new Provider
        {
            Name = $"it-{Guid.NewGuid():N}",
            ProviderType = ProviderTypes.OpenAi,
            BaseUrl = "https://api.test/v1",
            AuthType = AuthTypes.Bearer,
            ApiKeyCipher = cipherOverride ?? protector.Protect("sk-secret"),
            Enabled = true,
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        db.ProviderHealths.Add(new ProviderHealth { ProviderId = provider.Id, Status = HealthStatuses.Unknown });
        await db.SaveChangesAsync();
        return provider.Id;
    }

    private static ProviderConnectivityService NewService(ModelProviderDbContext db, ICredentialProtector protector, IModelProviderAdapter adapter, TestClock clock) =>
        new(db, protector, new StubFactory(adapter), clock);

    [Fact]
    public async Task TestConnection_Success_MarksHealthyWithLatency()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var protector = TestProtector.Create();
        var id = await SeedAsync(db, protector);
        var clock = new TestClock();
        var service = NewService(db, protector, new StubAdapter(Result<ConnectionTestResult>.Ok(new ConnectionTestResult { LatencyMs = 42 })), clock);

        var result = await service.TestConnectionAsync(id, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(HealthStatuses.Healthy, result.Data!.Status);
        Assert.Equal(42, result.Data.LatencyMs);
        Assert.Equal(0, result.Data.ConsecutiveFailures);
        Assert.Equal(clock.UtcNowEpochMs, result.Data.CheckedAt);

        await using var verifyDb = TestDb.NewContext();
        var stored = await verifyDb.ProviderHealths.AsNoTracking().FirstAsync(h => h.ProviderId == id);
        Assert.Equal(HealthStatuses.Healthy, stored.Status);
    }

    [Fact]
    public async Task TestConnection_ProbeFails_MarksUnhealthyAndCountsFailure()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var protector = TestProtector.Create();
        var id = await SeedAsync(db, protector);
        var service = NewService(db, protector, new StubAdapter(Result<ConnectionTestResult>.Fail(2002, "供应商返回 HTTP 401")), new TestClock());

        var result = await service.TestConnectionAsync(id, CancellationToken.None);

        Assert.Equal(200, result.Code); // 操作本身成功，记录的是 unhealthy
        Assert.Equal(HealthStatuses.Unhealthy, result.Data!.Status);
        Assert.Equal(1, result.Data.ConsecutiveFailures);
        Assert.Contains("401", result.Data.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestConnection_ProviderMissing_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var service = NewService(db, TestProtector.Create(), new StubAdapter(Result<ConnectionTestResult>.Ok(new ConnectionTestResult())), new TestClock());

        var result = await service.TestConnectionAsync(999_999_999, CancellationToken.None);

        Assert.Equal(2007, result.Code); // ProviderNotFound
    }

    [Fact]
    public async Task TestConnection_DecryptFailure_MarksUnhealthy()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var protector = TestProtector.Create();
        var id = await SeedAsync(db, protector, cipherOverride: "!!!not-base64!!!");
        var service = NewService(db, protector, new StubAdapter(Result<ConnectionTestResult>.Ok(new ConnectionTestResult())), new TestClock());

        var result = await service.TestConnectionAsync(id, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(HealthStatuses.Unhealthy, result.Data!.Status);
        Assert.Contains("解密", result.Data.LastError, StringComparison.Ordinal);
    }
}
