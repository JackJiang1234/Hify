using Hify.Contracts.ModelProvider;
using Hify.Modules.Conversation.Features.Chat;

namespace Hify.Modules.Conversation.Tests.Features.Chat;

/// <summary>滑动窗口裁剪的表驱动单测（纯函数，不连库）。预算用估算器统一口径计算。</summary>
public sealed class ContextWindowTrimmerTests
{
    private static readonly ITokenEstimator Estimator = new CharBasedTokenEstimator();

    private static ChatMessage Msg(string role, string content) => new() { Role = role, Content = content };

    private static int Cost(string content) => Estimator.Estimate(content);

    [Fact]
    public void Trim_EmptyHistory_ReturnsEmpty()
    {
        var result = ContextWindowTrimmer.Trim([], 1000, Estimator);
        Assert.Empty(result);
    }

    [Fact]
    public void Trim_NonPositiveBudget_ReturnsEmpty()
    {
        IReadOnlyList<ChatMessage> history = [Msg("user", "hello")];
        Assert.Empty(ContextWindowTrimmer.Trim(history, 0, Estimator));
        Assert.Empty(ContextWindowTrimmer.Trim(history, -5, Estimator));
    }

    [Fact]
    public void Trim_BudgetFitsAll_KeepsAllInOriginalOrder()
    {
        IReadOnlyList<ChatMessage> history = [Msg("user", "aaa"), Msg("assistant", "bbb"), Msg("user", "ccc")];
        var budget = Cost("aaa") + Cost("bbb") + Cost("ccc");

        var result = ContextWindowTrimmer.Trim(history, budget, Estimator);

        Assert.Equal(3, result.Count);
        Assert.Equal("aaa", result[0].Content);
        Assert.Equal("bbb", result[1].Content);
        Assert.Equal("ccc", result[2].Content);
    }

    [Fact]
    public void Trim_TightBudget_KeepsNewestDropsOldest()
    {
        IReadOnlyList<ChatMessage> history = [Msg("user", "oldest"), Msg("assistant", "middle"), Msg("user", "newest")];
        // 预算只够最新两条。
        var budget = Cost("newest") + Cost("middle");

        var result = ContextWindowTrimmer.Trim(history, budget, Estimator);

        Assert.Equal(2, result.Count);
        Assert.Equal("middle", result[0].Content);
        Assert.Equal("newest", result[1].Content);
    }

    [Fact]
    public void Trim_BudgetForOneButNewestIsLast_KeepsOnlyNewest()
    {
        IReadOnlyList<ChatMessage> history = [Msg("user", "old"), Msg("assistant", "new")];
        var budget = Cost("new");

        var result = ContextWindowTrimmer.Trim(history, budget, Estimator);

        Assert.Single(result);
        Assert.Equal("new", result[0].Content);
    }

    [Fact]
    public void Trim_NewestExceedsBudget_ReturnsEmpty()
    {
        IReadOnlyList<ChatMessage> history = [Msg("user", new string('x', 1000))];

        var result = ContextWindowTrimmer.Trim(history, 1, Estimator);

        Assert.Empty(result);
    }
}
