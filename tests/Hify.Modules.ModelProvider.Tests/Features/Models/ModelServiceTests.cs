using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Domain;
using Hify.Modules.ModelProvider.Features.Models;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Modules.ModelProvider.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.ModelProvider.Tests.Features.Models;

/// <summary>模型管理服务的真实库测试（连不上则跳过）。用唯一供应商隔离。</summary>
public sealed class ModelServiceTests : IAsyncLifetime
{
    private bool _available;

    public async Task InitializeAsync() => _available = await TestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<long> SeedProviderAsync(ModelProviderDbContext db)
    {
        var provider = new Provider { Name = $"it-{Guid.NewGuid():N}", ProviderType = ProviderTypes.OpenAi, BaseUrl = "https://api.test/v1" };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        return provider.Id;
    }

    private static CreateModelRequest NewChat(string name) => new()
    {
        Name = name,
        ModelType = ModelTypes.Chat,
        ContextWindow = 128000,
        MaxOutputTokens = 4096,
        Enabled = true,
    };

    [Fact]
    public async Task CreateAsync_UnderProvider_Persists()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var providerId = await SeedProviderAsync(db);
        var service = new ModelService(db);

        var result = await service.CreateAsync(providerId, NewChat("gpt-4o"), CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(providerId, result.Data!.ProviderId);
        Assert.Equal(ModelSources.Manual, result.Data.Source);
        Assert.False(result.Data.IsDefault);
    }

    [Fact]
    public async Task CreateAsync_ProviderMissing_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var result = await new ModelService(db).CreateAsync(999_999_999, NewChat("gpt-4o"), CancellationToken.None);

        Assert.Equal(2007, result.Code); // ProviderNotFound
    }

    [Fact]
    public async Task CreateAsync_DuplicateNameUnderProvider_ReturnsConflict()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var providerId = await SeedProviderAsync(db);
        var service = new ModelService(db);
        await service.CreateAsync(providerId, NewChat("gpt-4o"), CancellationToken.None);

        var second = await service.CreateAsync(providerId, NewChat("gpt-4o"), CancellationToken.None);

        Assert.Equal(2013, second.Code); // ModelNameConflict
    }

    [Fact]
    public async Task SetDefaultAsync_MovesDefaultToTargetWithinProviderAndType()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var providerId = await SeedProviderAsync(db);
        var service = new ModelService(db);
        var first = await service.CreateAsync(providerId, NewChat("gpt-4o"), CancellationToken.None);
        var second = await service.CreateAsync(providerId, NewChat("gpt-4o-mini"), CancellationToken.None);

        await service.SetDefaultAsync(first.Data!.Id, CancellationToken.None);
        await service.SetDefaultAsync(second.Data!.Id, CancellationToken.None);

        await using var verifyDb = TestDb.NewContext();
        var firstReloaded = await verifyDb.Models.AsNoTracking().FirstAsync(m => m.Id == first.Data.Id);
        var secondReloaded = await verifyDb.Models.AsNoTracking().FirstAsync(m => m.Id == second.Data.Id);
        Assert.False(firstReloaded.IsDefault);
        Assert.True(secondReloaded.IsDefault);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesModel()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var providerId = await SeedProviderAsync(db);
        var service = new ModelService(db);
        var created = await service.CreateAsync(providerId, NewChat("gpt-4o"), CancellationToken.None);

        await service.DeleteAsync(created.Data!.Id, CancellationToken.None);

        await using var verifyDb = TestDb.NewContext();
        Assert.Equal(2009, (await new ModelService(verifyDb).GetAsync(created.Data.Id, CancellationToken.None)).Code);
    }

    [Fact]
    public async Task ListByProviderAsync_ReturnsCreatedModels()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var providerId = await SeedProviderAsync(db);
        var service = new ModelService(db);
        await service.CreateAsync(providerId, NewChat("gpt-4o"), CancellationToken.None);
        await service.CreateAsync(providerId, NewChat("gpt-4o-mini"), CancellationToken.None);

        var result = await service.ListByProviderAsync(providerId, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(2, result.Data!.Count);
    }
}
