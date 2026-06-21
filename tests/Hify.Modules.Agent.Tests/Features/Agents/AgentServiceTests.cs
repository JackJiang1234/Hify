using Hify.Contracts.Agent;
using Hify.Contracts.ModelProvider;
using Hify.Modules.Agent.Features.Agents;
using Hify.Modules.Agent.Persistence;
using Hify.Modules.Agent.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Agent.Tests.Features.Agents;

/// <summary>Agent 服务的真实库测试（连不上则跳过）。引用校验用内存替身 IModelProviderQuery（方案 B）。</summary>
public sealed class AgentServiceTests : IAsyncLifetime
{
    private const long ChatModelId = 1;
    private bool _available;

    public async Task InitializeAsync() => _available = await AgentTestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static FakeModelProviderQuery FakeWithChatModel() =>
        new FakeModelProviderQuery().Add(FakeModelProviderQuery.ChatModel(ChatModelId));

    private static CreateAgentRequest NewAgent(string name) => new()
    {
        Name = name,
        ModelId = ChatModelId,
        SystemPrompt = "you are helpful",
        MaxIterations = 5,
        RetrievalParams = new RetrievalParams { TopK = 3 },
        ToolIds = [10, 11],
        KnowledgeBaseIds = [20],
    };

    private static string UniqueName() => $"it-{Guid.NewGuid():N}";

    [Fact]
    public async Task CreateAsync_Valid_PersistsWithBindings()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var service = new AgentService(db, FakeWithChatModel());

        var result = await service.CreateAsync(NewAgent(UniqueName()), CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(new long[] { 10, 11 }, result.Data!.ToolIds.OrderBy(x => x));
        Assert.Equal(new long[] { 20 }, result.Data.KnowledgeBaseIds);

        await using var verify = AgentTestDb.NewContext();
        Assert.Equal(2, await verify.AgentTools.CountAsync(t => t.AgentId == result.Data.Id));
        Assert.Equal(1, await verify.AgentKnowledges.CountAsync(k => k.AgentId == result.Data.Id));
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ReturnsConflict()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var service = new AgentService(db, FakeWithChatModel());
        var name = UniqueName();
        await service.CreateAsync(NewAgent(name), CancellationToken.None);

        var second = await service.CreateAsync(NewAgent(name), CancellationToken.None);

        Assert.Equal(3002, second.Code); // AgentNameConflict
    }

    [Fact]
    public async Task CreateAsync_ModelMissing_ReturnsModelInvalid()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var service = new AgentService(db, new FakeModelProviderQuery()); // 未预置任何模型

        var result = await service.CreateAsync(NewAgent(UniqueName()), CancellationToken.None);

        Assert.Equal(3003, result.Code); // AgentModelInvalid
    }

    [Fact]
    public async Task CreateAsync_ModelNotChat_ReturnsModelInvalid()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var fake = new FakeModelProviderQuery().Add(new ModelDto { Id = ChatModelId, ModelType = ModelTypes.Embedding, Enabled = true });
        var service = new AgentService(db, fake);

        var result = await service.CreateAsync(NewAgent(UniqueName()), CancellationToken.None);

        Assert.Equal(3003, result.Code);
    }

    [Fact]
    public async Task CreateAsync_ModelDisabled_ReturnsModelInvalid()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var fake = new FakeModelProviderQuery().Add(FakeModelProviderQuery.ChatModel(ChatModelId, enabled: false));
        var service = new AgentService(db, fake);

        var result = await service.CreateAsync(NewAgent(UniqueName()), CancellationToken.None);

        Assert.Equal(3003, result.Code);
    }

    [Fact]
    public async Task CreateAsync_ToolsBoundButModelNoToolSupport_ReturnsToolUnsupported()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var fake = new FakeModelProviderQuery().Add(FakeModelProviderQuery.ChatModel(ChatModelId, supportsTools: false));
        var service = new AgentService(db, fake);

        var result = await service.CreateAsync(NewAgent(UniqueName()), CancellationToken.None);

        Assert.Equal(3006, result.Code); // ModelToolUnsupported
    }

    [Fact]
    public async Task CreateAsync_NoTools_ModelWithoutToolSupport_Passes()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var fake = new FakeModelProviderQuery().Add(FakeModelProviderQuery.ChatModel(ChatModelId, supportsTools: false));
        var service = new AgentService(db, fake);

        var result = await service.CreateAsync(NewAgent(UniqueName()) with { ToolIds = [] }, CancellationToken.None);

        Assert.Equal(200, result.Code);
    }

    [Fact]
    public async Task CreateAsync_MaxTokensExceedsModel_ReturnsModelInvalid()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var fake = new FakeModelProviderQuery().Add(FakeModelProviderQuery.ChatModel(ChatModelId, maxOutputTokens: 100));
        var service = new AgentService(db, fake);
        var request = NewAgent(UniqueName()) with { ModelParams = new ModelParams { MaxTokens = 200 } };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.Equal(3003, result.Code);
    }

    [Fact]
    public async Task UpdateAsync_Missing_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var service = new AgentService(db, FakeWithChatModel());
        var update = new UpdateAgentRequest { Name = UniqueName(), ModelId = ChatModelId, MaxIterations = 5 };

        var result = await service.UpdateAsync(999_999_999, update, CancellationToken.None);

        Assert.Equal(3001, result.Code); // AgentNotFound
    }

    [Fact]
    public async Task UpdateAsync_ReplacesBindings()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var service = new AgentService(db, FakeWithChatModel());
        var created = await service.CreateAsync(NewAgent(UniqueName()), CancellationToken.None);

        var update = new UpdateAgentRequest
        {
            Name = created.Data!.Name,
            ModelId = ChatModelId,
            MaxIterations = 5,
            RetrievalParams = new RetrievalParams { TopK = 3 },
            ToolIds = [11, 12], // 删 10、留 11、增 12
            KnowledgeBaseIds = [],
        };

        var result = await service.UpdateAsync(created.Data.Id, update, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(new long[] { 11, 12 }, result.Data!.ToolIds.OrderBy(x => x));
        Assert.Empty(result.Data.KnowledgeBaseIds);

        await using var verify = AgentTestDb.NewContext();
        var activeTools = await verify.AgentTools.Where(t => t.AgentId == created.Data.Id).Select(t => t.ToolId).ToListAsync();
        Assert.Equal(new long[] { 11, 12 }, activeTools.OrderBy(x => x));
    }

    [Fact]
    public async Task DeleteAsync_CascadeSoftDeletesBindings()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var service = new AgentService(db, FakeWithChatModel());
        var created = await service.CreateAsync(NewAgent(UniqueName()), CancellationToken.None);

        var deleted = await service.DeleteAsync(created.Data!.Id, CancellationToken.None);
        Assert.Equal(200, deleted.Code);

        await using var verify = AgentTestDb.NewContext();
        Assert.Equal(3001, (await new AgentService(verify, FakeWithChatModel()).GetAsync(created.Data.Id, CancellationToken.None)).Code);
        Assert.Equal(0, await verify.AgentTools.CountAsync(t => t.AgentId == created.Data.Id));
        Assert.Equal(0, await verify.AgentKnowledges.CountAsync(k => k.AgentId == created.Data.Id));
    }

    [Fact]
    public async Task GetAsync_RoundTripsParams()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var service = new AgentService(db, FakeWithChatModel());
        var request = NewAgent(UniqueName()) with
        {
            ModelParams = new ModelParams { Temperature = 0.7, MaxTokens = 1024 },
            RetrievalParams = new RetrievalParams { TopK = 5, ScoreThreshold = 0.42 },
        };
        var created = await service.CreateAsync(request, CancellationToken.None);

        await using var verify = AgentTestDb.NewContext();
        var fetched = await new AgentService(verify, FakeWithChatModel()).GetAsync(created.Data!.Id, CancellationToken.None);

        Assert.Equal(200, fetched.Code);
        Assert.Equal(0.7, fetched.Data!.ModelParams.Temperature);
        Assert.Equal(1024, fetched.Data.ModelParams.MaxTokens);
        Assert.Equal(5, fetched.Data.RetrievalParams.TopK);
        Assert.Equal(0.42, fetched.Data.RetrievalParams.ScoreThreshold);
    }
}
