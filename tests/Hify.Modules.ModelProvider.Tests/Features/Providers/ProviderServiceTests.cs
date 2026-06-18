using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Features.Providers;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Modules.ModelProvider.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.ModelProvider.Tests.Features.Providers;

/// <summary>
/// 供应商 CRUD 服务的真实库测试（连不上则跳过）。用唯一名隔离，库为一次性验证库不清理残留。
/// </summary>
public sealed class ProviderServiceTests : IAsyncLifetime
{
    private bool _available;

    public async Task InitializeAsync() => _available = await TestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static ProviderService NewService(ModelProviderDbContext db) => new(db, TestProtector.Create());

    private static CreateProviderRequest NewCreate(string name) => new()
    {
        Name = name,
        ProviderType = ProviderTypes.OpenAi,
        BaseUrl = "https://api.test/v1",
        AuthType = AuthTypes.Bearer,
        ApiKey = "sk-secret-123456",
        Enabled = true,
    };

    private static string UniqueName() => $"it-{Guid.NewGuid():N}";

    [Fact]
    public async Task CreateAsync_PersistsProviderAndHealthRow_EncryptsKey()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var result = await NewService(db).CreateAsync(NewCreate(UniqueName()), CancellationToken.None);

        Assert.Equal(200, result.Code);
        var dto = result.Data!;
        Assert.Equal("…3456", dto.ApiKeyHint);
        Assert.Equal(HealthStatuses.Unknown, dto.Health.Status);

        await using var verifyDb = TestDb.NewContext();
        var health = await verifyDb.ProviderHealths.AsNoTracking().FirstOrDefaultAsync(h => h.ProviderId == dto.Id);
        Assert.NotNull(health);

        var stored = await verifyDb.Providers.AsNoTracking().FirstAsync(p => p.Id == dto.Id);
        Assert.NotEqual("sk-secret-123456", stored.ApiKeyCipher); // 落库为密文
        Assert.Equal("sk-secret-123456", TestProtector.Create().Unprotect(stored.ApiKeyCipher));
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ReturnsConflict()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var service = NewService(db);
        var name = UniqueName();
        await service.CreateAsync(NewCreate(name), CancellationToken.None);

        var second = await service.CreateAsync(NewCreate(name), CancellationToken.None);

        Assert.Equal(2008, second.Code); // ProviderNameConflict
    }

    [Fact]
    public async Task GetAsync_Missing_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var result = await NewService(db).GetAsync(999_999_999, CancellationToken.None);

        Assert.Equal(2007, result.Code); // ProviderNotFound
    }

    [Fact]
    public async Task ListAsync_ReturnsCreatedProviderWithHealth()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var service = NewService(db);
        var created = await service.CreateAsync(NewCreate(UniqueName()), CancellationToken.None);

        var page = await service.ListAsync(1, 20, CancellationToken.None);

        Assert.Equal(200, page.Code);
        Assert.True(page.Total >= 1);
        var listed = page.Data!.FirstOrDefault(p => p.Id == created.Data!.Id);
        Assert.NotNull(listed);
        Assert.Equal(HealthStatuses.Unknown, listed!.Health.Status);
    }

    [Fact]
    public async Task UpdateAsync_KeepsKeyWhenApiKeyEmpty_AndReplacesFields()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var service = NewService(db);
        var created = await service.CreateAsync(NewCreate(UniqueName()), CancellationToken.None);
        var id = created.Data!.Id;

        await using var readDb = TestDb.NewContext();
        var originalCipher = (await readDb.Providers.AsNoTracking().FirstAsync(p => p.Id == id)).ApiKeyCipher;

        var update = new UpdateProviderRequest
        {
            Name = created.Data.Name,
            ProviderType = ProviderTypes.OpenAi,
            BaseUrl = "https://changed.test/v1",
            AuthType = AuthTypes.Bearer,
            ApiKey = string.Empty, // 留空保留原密钥
            Settings = "{}",
            Enabled = true,
        };
        var result = await service.UpdateAsync(id, update, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("https://changed.test/v1", result.Data!.BaseUrl);

        await using var verifyDb = TestDb.NewContext();
        var stored = await verifyDb.Providers.AsNoTracking().FirstAsync(p => p.Id == id);
        Assert.Equal(originalCipher, stored.ApiKeyCipher); // 未改动
    }

    [Fact]
    public async Task UpdateAsync_ReencryptsWhenApiKeyProvided()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var service = NewService(db);
        var created = await service.CreateAsync(NewCreate(UniqueName()), CancellationToken.None);

        var update = new UpdateProviderRequest
        {
            Name = created.Data!.Name,
            ProviderType = ProviderTypes.OpenAi,
            BaseUrl = created.Data.BaseUrl,
            AuthType = AuthTypes.Bearer,
            ApiKey = "sk-rotated-9999",
            Settings = "{}",
            Enabled = true,
        };
        var result = await service.UpdateAsync(created.Data.Id, update, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal("…9999", result.Data!.ApiKeyHint);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesProviderAndHealth()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var service = NewService(db);
        var created = await service.CreateAsync(NewCreate(UniqueName()), CancellationToken.None);
        var id = created.Data!.Id;

        var deleted = await service.DeleteAsync(id, CancellationToken.None);
        Assert.Equal(200, deleted.Code);

        await using var verifyDb = TestDb.NewContext();
        Assert.Equal(2007, (await NewService(verifyDb).GetAsync(id, CancellationToken.None)).Code);
        Assert.Null(await verifyDb.ProviderHealths.AsNoTracking().FirstOrDefaultAsync(h => h.ProviderId == id));
    }

    [Fact]
    public async Task SetEnabledAsync_TogglesFlag()
    {
        if (!_available)
        {
            return;
        }

        await using var db = TestDb.NewContext();
        var service = NewService(db);
        var created = await service.CreateAsync(NewCreate(UniqueName()), CancellationToken.None);
        var id = created.Data!.Id;

        await service.SetEnabledAsync(id, enabled: false, CancellationToken.None);

        await using var verifyDb = TestDb.NewContext();
        var dto = await NewService(verifyDb).GetAsync(id, CancellationToken.None);
        Assert.False(dto.Data!.Enabled);
    }
}
