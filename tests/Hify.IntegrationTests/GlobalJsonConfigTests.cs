using System.Text.Json;

namespace Hify.IntegrationTests;

public class GlobalJsonConfigTests : IClassFixture<HifyTestFactory>
{
    private readonly HifyTestFactory _factory;

    public GlobalJsonConfigTests(HifyTestFactory factory) => _factory = factory;

    [Fact]
    public async Task GlobalSerialization_UsesCamelCaseKeys_AndKeepsNulls()
    {
        var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/__test/json");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Result 外层：camelCase
        Assert.Equal(200, root.GetProperty("code").GetInt32());
        Assert.Equal("success", root.GetProperty("message").GetString());

        // data 内层：多词属性 camelCase + 保留 null
        var data = root.GetProperty("data");
        Assert.Equal("hify", data.GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("note").ValueKind);
    }

    [Fact]
    public async Task InternalController_IsDiscovered_AndRoutable()
    {
        var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/__test/internal");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("internal-ok", doc.RootElement.GetProperty("data").GetString());
    }
}
