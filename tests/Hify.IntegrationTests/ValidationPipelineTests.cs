using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hify.IntegrationTests;

public class ValidationPipelineTests : IClassFixture<HifyTestFactory>
{
    private readonly HifyTestFactory _factory;

    public ValidationPipelineTests(HifyTestFactory factory) => _factory = factory;

    [Fact]
    public async Task InvalidRequest_ShortCircuits_WithParamInvalid()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/__test/validation", new { name = "", count = 0 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(1001, root.GetProperty("code").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("data").ValueKind);
        Assert.Contains("name", root.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ValidRequest_PassesThrough_ToAction()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/__test/validation", new { name = "hify", count = 3 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(200, root.GetProperty("code").GetInt32());
        Assert.Equal("hify", root.GetProperty("data").GetProperty("name").GetString());
        Assert.Equal(3, root.GetProperty("data").GetProperty("count").GetInt32());
    }
}
