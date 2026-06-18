using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Domain;
using Hify.Modules.ModelProvider.Features.Models;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Modules.ModelProvider.Tests.Support;

namespace Hify.Modules.ModelProvider.Tests.Features.Models;

/// <summary>IModelProviderQuery 实现的真实库测试（连不上则跳过）。</summary>
public sealed class ModelProviderQueryTests : IAsyncLifetime
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

    [Fact]
    public async Task GetModelAsync_Found_ReturnsDto()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var providerId = await SeedProviderAsync(db);
        var model = new Model { ProviderId = providerId, Name = "gpt-4o", ModelType = ModelTypes.Chat, Enabled = true };
        db.Models.Add(model);
        await db.SaveChangesAsync();

        var result = await new ModelProviderQuery(db).GetModelAsync(model.Id, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("gpt-4o", result.Data!.Name);
    }

    [Fact]
    public async Task GetModelAsync_Missing_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var result = await new ModelProviderQuery(db).GetModelAsync(999_999_999, CancellationToken.None);

        Assert.Equal(2009, result.Code); // ModelNotFound
    }

    [Fact]
    public async Task GetDefaultModelAsync_ReturnsEnabledDefaultOfType()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var providerId = await SeedProviderAsync(db);
        db.Models.Add(new Model { ProviderId = providerId, Name = "gpt-4o", ModelType = ModelTypes.Chat, Enabled = true, IsDefault = true });
        db.Models.Add(new Model { ProviderId = providerId, Name = "gpt-4o-mini", ModelType = ModelTypes.Chat, Enabled = true, IsDefault = false });
        await db.SaveChangesAsync();

        var result = await new ModelProviderQuery(db).GetDefaultModelAsync(providerId, ModelTypes.Chat, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("gpt-4o", result.Data!.Name);
        Assert.True(result.Data.IsDefault);
    }

    [Fact]
    public async Task GetDefaultModelAsync_NoDefault_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var providerId = await SeedProviderAsync(db);

        var result = await new ModelProviderQuery(db).GetDefaultModelAsync(providerId, ModelTypes.Chat, CancellationToken.None);

        Assert.Equal(2009, result.Code);
    }
}
