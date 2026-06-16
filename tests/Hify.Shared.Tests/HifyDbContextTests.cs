using Hify.Shared.Persistence;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;

namespace Hify.Shared.Tests;

public class HifyDbContextTests
{
    private enum Color
    {
        Red,
        Green,
    }

    private sealed class Sample : EntityBase
    {
        public string Name { get; set; } = "";

        public Color Color { get; set; }
    }

    private sealed class SampleDbContext : HifyDbContext
    {
        public SampleDbContext(DbContextOptions options, IClock clock)
            : base(options, clock)
        {
        }

        public DbSet<Sample> Samples => Set<Sample>();
    }

    private sealed class FixedClock : IClock
    {
        public long UtcNowEpochMs { get; set; }
    }

    private static SampleDbContext NewContext(IClock clock)
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase($"hify-{Guid.NewGuid()}")
            .Options;
        return new SampleDbContext(options, clock);
    }

    [Fact]
    public async Task SaveChanges_OnAdd_FillsCreatedAndUpdated()
    {
        var clock = new FixedClock { UtcNowEpochMs = 1000 };
        await using var db = NewContext(clock);

        var sample = new Sample { Name = "a" };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        Assert.Equal(1000, sample.CreatedAt);
        Assert.Equal(1000, sample.UpdatedAt);
        Assert.Equal(0, sample.DeletedAt);
    }

    [Fact]
    public async Task SaveChanges_OnModify_UpdatesOnlyUpdatedAt()
    {
        var clock = new FixedClock { UtcNowEpochMs = 1000 };
        await using var db = NewContext(clock);
        var sample = new Sample { Name = "a" };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        clock.UtcNowEpochMs = 2000;
        sample.Name = "b";
        await db.SaveChangesAsync();

        Assert.Equal(1000, sample.CreatedAt);
        Assert.Equal(2000, sample.UpdatedAt);
    }

    [Fact]
    public async Task SaveChanges_OnRemove_ConvertsToSoftDelete()
    {
        var clock = new FixedClock { UtcNowEpochMs = 1000 };
        await using var db = NewContext(clock);
        var sample = new Sample { Name = "a" };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        clock.UtcNowEpochMs = 3000;
        db.Samples.Remove(sample);
        await db.SaveChangesAsync();

        // 行仍存在（软删），但被全局过滤排除；忽略过滤可见且 deleted_at 已置。
        Assert.Empty(await db.Samples.ToListAsync());
        var raw = await db.Samples.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(3000, raw.DeletedAt);
    }

    [Fact]
    public void Model_AppliesEnumAsVarcharConvention()
    {
        var clock = new FixedClock { UtcNowEpochMs = 1 };
        using var db = NewContext(clock);

        var property = db.Model
            .FindEntityType(typeof(Sample))!
            .FindProperty(nameof(Sample.Color))!;

        Assert.NotNull(property.GetValueConverter());
        Assert.Equal(ModelBuilderConventions.EnumColumnLength, property.GetMaxLength());
    }
}
