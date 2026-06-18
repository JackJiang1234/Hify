using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Adapters;
using Hify.Modules.ModelProvider.Domain;
using Hify.Modules.ModelProvider.Features.Providers;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Modules.ModelProvider.Security;
using Hify.Modules.ModelProvider.Tests.Support;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hify.Modules.ModelProvider.Tests.Features.Providers;

/// <summary>周期探活单轮逻辑的真实库测试（连不上则跳过）：只探启用中的供应商，停用的不动。</summary>
public sealed class ProviderHealthProbeTests : IAsyncLifetime
{
    private bool _available;

    public async Task InitializeAsync() => _available = await TestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class OkAdapter : IModelProviderAdapter
    {
        public string ProviderType => ProviderTypes.OpenAi;

        public Task<Result<ConnectionTestResult>> TestConnectionAsync(ProviderConnection connection, CancellationToken cancellationToken) =>
            Task.FromResult(Result<ConnectionTestResult>.Ok(new ConnectionTestResult { LatencyMs = 5 }));

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

    private static async Task<long> SeedAsync(ModelProviderDbContext db, ICredentialProtector protector, bool enabled)
    {
        var provider = new Provider
        {
            Name = $"it-{Guid.NewGuid():N}",
            ProviderType = ProviderTypes.OpenAi,
            BaseUrl = "https://api.test/v1",
            AuthType = AuthTypes.Bearer,
            ApiKeyCipher = protector.Protect("sk-secret"),
            Enabled = enabled,
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        db.ProviderHealths.Add(new ProviderHealth { ProviderId = provider.Id, Status = HealthStatuses.Unknown });
        await db.SaveChangesAsync();
        return provider.Id;
    }

    [Fact]
    public async Task ProbeAllAsync_ProbesEnabledProviders_SkipsDisabled()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var protector = TestProtector.Create();
        var enabledId = await SeedAsync(db, protector, enabled: true);
        var disabledId = await SeedAsync(db, protector, enabled: false);

        var connectivity = new ProviderConnectivityService(db, protector, new StubFactory(new OkAdapter()), new TestClock());
        var probe = new ProviderHealthProbe(db, connectivity, NullLogger<ProviderHealthProbe>.Instance);

        var probed = await probe.ProbeAllAsync(CancellationToken.None);

        Assert.True(probed >= 1);

        await using var verifyDb = TestDb.NewContext();
        var enabledHealth = await verifyDb.ProviderHealths.AsNoTracking().FirstAsync(h => h.ProviderId == enabledId);
        var disabledHealth = await verifyDb.ProviderHealths.AsNoTracking().FirstAsync(h => h.ProviderId == disabledId);

        Assert.Equal(HealthStatuses.Healthy, enabledHealth.Status); // 启用的被探活
        Assert.Equal(HealthStatuses.Unknown, disabledHealth.Status); // 停用的未动
    }
}
