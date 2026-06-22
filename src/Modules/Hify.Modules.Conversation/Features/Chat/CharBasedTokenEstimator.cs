namespace Hify.Modules.Conversation.Features.Chat;

/// <summary>
/// 字符数粗估 token（一期，设计决策 B）：<c>tokens ≈ ceil(字符数 / 系数)</c>，外加每条消息固定开销
/// （角色标记等）。系数偏小以保守高估，避免低估导致超窗。每条消息至少计 1 个开销 token。
/// </summary>
internal sealed class CharBasedTokenEstimator : ITokenEstimator
{
    // 每字符约 1/CharsPerToken 个 token。取 3 偏保守（中英文混排时高于真实值，留余量）。
    private const int CharsPerToken = 3;

    // 每条消息的固定结构开销（角色/分隔符），计入预算避免低估。
    private const int PerMessageOverhead = 4;

    /// <inheritdoc />
    public int Estimate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var contentTokens = (text.Length + CharsPerToken - 1) / CharsPerToken;
        return contentTokens + PerMessageOverhead;
    }
}
