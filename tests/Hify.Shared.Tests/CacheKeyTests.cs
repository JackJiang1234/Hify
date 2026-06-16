using Hify.Shared.Caching;

namespace Hify.Shared.Tests;

public class CacheKeyTests
{
    [Theory]
    [InlineData("provider", "config", 5L, "hify:provider:config:5")]
    [InlineData("agent", "detail", 42L, "hify:agent:detail:42")]
    [InlineData("conversation", "context", "abc", "hify:conversation:context:abc")]
    public void For_WithId_BuildsPrefixedKey(string module, string entity, object id, string expected)
    {
        Assert.Equal(expected, CacheKey.For(module, entity, id));
    }

    [Theory]
    [InlineData("agent", "list", "hify:agent:list")]
    [InlineData("provider", "all", "hify:provider:all")]
    public void For_WithoutId_BuildsCollectionKey(string module, string entity, string expected)
    {
        Assert.Equal(expected, CacheKey.For(module, entity));
    }
}
