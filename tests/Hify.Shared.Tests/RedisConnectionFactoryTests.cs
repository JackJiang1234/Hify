using System.Net;

using Hify.Shared.Caching;
using Hify.Shared.Configuration;

namespace Hify.Shared.Tests;

public class RedisConnectionFactoryTests
{
    [Fact]
    public void BuildConfiguration_MapsOptions()
    {
        var options = new RedisOptions
        {
            Host = "redis-host",
            Port = 6380,
            Database = 3,
            ConnectTimeoutMs = 1234,
            Password = "",
        };

        var configuration = RedisConnectionFactory.BuildConfiguration(options);

        Assert.Equal(3, configuration.DefaultDatabase!.Value);
        Assert.Equal(1234, configuration.ConnectTimeout);
        Assert.False(configuration.AbortOnConnectFail);
        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(configuration.EndPoints));
        Assert.Equal("redis-host", endpoint.Host);
        Assert.Equal(6380, endpoint.Port);
    }

    [Fact]
    public void BuildConfiguration_EmptyPassword_LeavesPasswordUnset()
    {
        var options = new RedisOptions { Host = "h", Password = "" };

        var configuration = RedisConnectionFactory.BuildConfiguration(options);

        Assert.Null(configuration.Password);
    }

    [Fact]
    public void BuildConfiguration_WithPassword_SetsPassword()
    {
        var options = new RedisOptions { Host = "h", Password = "secret" };

        var configuration = RedisConnectionFactory.BuildConfiguration(options);

        Assert.Equal("secret", configuration.Password);
    }
}
