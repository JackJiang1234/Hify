using Hify.Shared.Persistence;

namespace Hify.Shared.Tests;

public class SnakeCaseNamingTests
{
    [Theory]
    [InlineData("Id", "id")]
    [InlineData("CreatedAt", "created_at")]
    [InlineData("DeletedAt", "deleted_at")]
    [InlineData("ConversationId", "conversation_id")]
    [InlineData("ModelProvider", "model_provider")]
    [InlineData("HttpClient", "http_client")]
    [InlineData("already_snake", "already_snake")]
    [InlineData("Vector1536", "vector1536")]
    [InlineData("APIKey", "api_key")]
    public void ToSnakeCase_ConvertsPascalCase(string input, string expected)
    {
        Assert.Equal(expected, SnakeCaseNaming.ToSnakeCase(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ToSnakeCase_EmptyOrWhitespace_ReturnsAsIs(string input)
    {
        Assert.Equal(input, SnakeCaseNaming.ToSnakeCase(input));
    }
}
