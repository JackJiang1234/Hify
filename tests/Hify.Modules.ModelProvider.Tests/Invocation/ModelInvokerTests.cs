using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Adapters;
using Hify.Modules.ModelProvider.Domain;
using Hify.Modules.ModelProvider.Invocation;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Modules.ModelProvider.Security;
using Hify.Modules.ModelProvider.Tests.Support;
using Hify.Shared.Results;

namespace Hify.Modules.ModelProvider.Tests.Invocation;

/// <summary>
/// IModelInvoker 解析与分发的真实库测试（连不上则跳过）：解密密钥、选适配器、错误码。
/// 适配器用 stub，不发真实 HTTP。
/// </summary>
public sealed class ModelInvokerTests : IAsyncLifetime
{
    private bool _available;

    public async Task InitializeAsync() => _available = await TestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class StubAdapter : IModelProviderAdapter
    {
        public ProviderConnection? SeenConnection { get; private set; }

        public string? SeenModel { get; private set; }

        public string ProviderType => ProviderTypes.OpenAi;

        public Task<Result<ConnectionTestResult>> TestConnectionAsync(ProviderConnection connection, CancellationToken cancellationToken) =>
            Task.FromResult(Result<ConnectionTestResult>.Ok(new ConnectionTestResult { LatencyMs = 1 }));

        public Task<Result<ChatResponse>> ChatAsync(ProviderConnection connection, string model, ChatRequest request, CancellationToken cancellationToken)
        {
            SeenConnection = connection;
            SeenModel = model;
            return Task.FromResult(Result<ChatResponse>.Ok(new ChatResponse { Content = "ok" }));
        }

        public Task<Result<IAsyncEnumerable<ChatStreamChunk>>> ChatStreamAsync(ProviderConnection connection, string model, ChatRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<EmbeddingResponse>> EmbedAsync(ProviderConnection connection, string model, EmbeddingRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Result<EmbeddingResponse>.Ok(new EmbeddingResponse()));
    }

    private sealed class StubFactory(IModelProviderAdapter adapter) : IModelProviderAdapterFactory
    {
        public IModelProviderAdapter Get(string providerType) => adapter;
    }

    private static async Task<long> SeedAsync(
        ModelProviderDbContext db,
        ICredentialProtector protector,
        bool providerEnabled = true,
        bool modelEnabled = true)
    {
        var provider = new Provider
        {
            Name = $"it-{Guid.NewGuid():N}",
            ProviderType = ProviderTypes.OpenAi,
            BaseUrl = "http://localhost/v1",
            AuthType = AuthTypes.Bearer,
            ApiKeyCipher = protector.Protect("sk-secret"),
            Enabled = providerEnabled,
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        var model = new Model
        {
            ProviderId = provider.Id,
            Name = "gpt-4o",
            ModelType = ModelTypes.Chat,
            Enabled = modelEnabled,
        };
        db.Models.Add(model);
        await db.SaveChangesAsync();
        return model.Id;
    }

    private static ChatRequest SampleRequest() =>
        new() { Messages = [new ChatMessage { Role = "user", Content = "hi" }], MaxTokens = 16 };

    [Fact]
    public async Task ChatAsync_ResolvesDecryptsKeyAndDispatchesToAdapter()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var protector = TestProtector.Create();
        var modelId = await SeedAsync(db, protector);
        var stub = new StubAdapter();
        var invoker = new ModelInvoker(db, protector, new StubFactory(stub));

        var result = await invoker.ChatAsync(modelId, SampleRequest(), CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("ok", result.Data!.Content);
        Assert.Equal("gpt-4o", stub.SeenModel);
        Assert.Equal("sk-secret", stub.SeenConnection!.ApiKey); // 已解密
    }

    [Fact]
    public async Task ChatAsync_ModelNotFound_Returns2009()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var invoker = new ModelInvoker(db, TestProtector.Create(), new StubFactory(new StubAdapter()));

        var result = await invoker.ChatAsync(999_999_999, SampleRequest(), CancellationToken.None);

        Assert.Equal(2009, result.Code); // ModelNotFound
    }

    [Fact]
    public async Task ChatAsync_ProviderDisabled_Returns2010()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var protector = TestProtector.Create();
        var modelId = await SeedAsync(db, protector, providerEnabled: false);
        var invoker = new ModelInvoker(db, protector, new StubFactory(new StubAdapter()));

        var result = await invoker.ChatAsync(modelId, SampleRequest(), CancellationToken.None);

        Assert.Equal(2010, result.Code); // ProviderDisabled
    }

    [Fact]
    public async Task ChatAsync_ModelDisabled_Returns2011()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var protector = TestProtector.Create();
        var modelId = await SeedAsync(db, protector, modelEnabled: false);
        var invoker = new ModelInvoker(db, protector, new StubFactory(new StubAdapter()));

        var result = await invoker.ChatAsync(modelId, SampleRequest(), CancellationToken.None);

        Assert.Equal(2011, result.Code); // ModelDisabled
    }
}
