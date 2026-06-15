using Hify.Shared.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hify.IntegrationTests;

public class ConfigurationBindingTests : IClassFixture<HifyTestFactory>
{
    private readonly HifyTestFactory _factory;

    public ConfigurationBindingTests(HifyTestFactory factory) => _factory = factory;

    [Fact]
    public void DatabaseOptions_BindFromConfig_AndPassValidation()
    {
        // 访问 Services 会触发 host 启动与 ValidateOnStart；校验失败会在此抛出。
        var options = _factory.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        Assert.Equal("localhost", options.Host);
        Assert.Equal("hify_test", options.Database);
        Assert.Equal("hify", options.Username);
        Assert.Equal("test-secret", options.Password);
        Assert.Equal(5432, options.Port);
        Assert.Equal(50, options.MaxPoolSize);
    }

    [Fact]
    public void RedisOptions_BindFromConfig_AndPassValidation()
    {
        var options = _factory.Services.GetRequiredService<IOptions<RedisOptions>>().Value;

        Assert.Equal("localhost", options.Host);
        Assert.Equal(6379, options.Port);
        Assert.Equal(0, options.Database);
        Assert.Equal(5000, options.ConnectTimeoutMs);
    }
}
