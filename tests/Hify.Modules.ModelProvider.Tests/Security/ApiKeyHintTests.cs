using Hify.Modules.ModelProvider.Security;

namespace Hify.Modules.ModelProvider.Tests.Security;

/// <summary>密钥脱敏提示：仅末 4 位，过短全掩码，空串保持空。</summary>
public sealed class ApiKeyHintTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("ab", "…")]
    [InlineData("abcd", "…")]
    [InlineData("sk-abcd1234", "…1234")]
    [InlineData("0123456789", "…6789")]
    public void Of_MasksAllButLastFour(string apiKey, string expected)
    {
        Assert.Equal(expected, ApiKeyHint.Of(apiKey));
    }
}
