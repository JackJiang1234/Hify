using Hify.Modules.ModelProvider.Domain;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hify.Modules.ModelProvider.Tests.Persistence;

/// <summary>
/// DbContext 映射的离线断言（不连真实库）：验证三表落在 model_provider schema、
/// 列名 snake_case、settings 为 jsonb。落库行为（软删过滤、唯一约束）在真实库集成测试中验证。
/// </summary>
public sealed class ModelProviderDbContextMappingTests
{
    private sealed class FixedClock : IClock
    {
        public long UtcNowEpochMs => 0;
    }

    private static ModelProviderDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ModelProviderDbContext>()
            .UseNpgsql("Host=localhost;Database=hify;Username=hify;Password=placeholder")
            .Options;
        return new ModelProviderDbContext(options, new FixedClock());
    }

    [Theory]
    [InlineData(typeof(Provider), "provider")]
    [InlineData(typeof(Model), "model")]
    [InlineData(typeof(ProviderHealth), "provider_health")]
    public void Entity_MapsToModelProviderSchema_WithSnakeCaseTable(Type clrType, string expectedTable)
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(clrType);

        Assert.NotNull(entityType);
        Assert.Equal("model_provider", entityType!.GetSchema());
        Assert.Equal(expectedTable, entityType.GetTableName());
    }

    [Theory]
    [InlineData(nameof(Provider.ApiKeyCipher), "api_key_cipher")]
    [InlineData(nameof(Provider.AuthHeaderName), "auth_header_name")]
    [InlineData(nameof(Provider.ProviderType), "provider_type")]
    public void ProviderColumns_AreSnakeCased(string propertyName, string expectedColumn)
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Provider))!;
        var store = StoreObjectIdentifier.Table("provider", "model_provider");

        var column = entityType.FindProperty(propertyName)!.GetColumnName(store);

        Assert.Equal(expectedColumn, column);
    }

    [Fact]
    public void ProviderSettings_IsJsonbColumn()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Provider))!;

        var columnType = entityType.FindProperty(nameof(Provider.Settings))!.GetColumnType();

        Assert.Equal("jsonb", columnType);
    }
}
