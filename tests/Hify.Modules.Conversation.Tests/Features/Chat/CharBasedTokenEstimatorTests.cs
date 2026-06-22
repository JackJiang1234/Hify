using Hify.Modules.Conversation.Features.Chat;

namespace Hify.Modules.Conversation.Tests.Features.Chat;

/// <summary>字符粗估器的表驱动单测：内容按 ceil(len/3) 估算 + 每条固定开销 4。</summary>
public sealed class CharBasedTokenEstimatorTests
{
    private static readonly ITokenEstimator Estimator = new CharBasedTokenEstimator();

    [Theory]
    [InlineData("", 4)]        // 仅开销
    [InlineData("a", 5)]       // ceil(1/3)=1 + 4
    [InlineData("abc", 5)]     // ceil(3/3)=1 + 4
    [InlineData("abcd", 6)]    // ceil(4/3)=2 + 4
    [InlineData("abcdef", 6)]  // ceil(6/3)=2 + 4
    public void Estimate_FollowsCharsPerTokenPlusOverhead(string text, int expected)
    {
        Assert.Equal(expected, Estimator.Estimate(text));
    }

    [Fact]
    public void Estimate_LongerText_CostsMore()
    {
        Assert.True(Estimator.Estimate(new string('x', 300)) > Estimator.Estimate(new string('x', 30)));
    }
}
