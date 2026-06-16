using System.Net;
using System.Text.Json;

namespace Hify.IntegrationTests;

public class HealthCheckTests : IClassFixture<HifyTestFactory>
{
    private readonly HifyTestFactory _factory;

    public HealthCheckTests(HifyTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsHttp200_WithUnifiedResultBody()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(200, root.GetProperty("code").GetInt32());
        Assert.Equal("healthy", root.GetProperty("message").GetString());

        var data = root.GetProperty("data");
        Assert.Equal("Healthy", data.GetProperty("status").GetString());
        // self 检查项存在且健康
        var selfCheck = data.GetProperty("checks").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == "self");
        Assert.Equal("Healthy", selfCheck.GetProperty("status").GetString());
    }
}
