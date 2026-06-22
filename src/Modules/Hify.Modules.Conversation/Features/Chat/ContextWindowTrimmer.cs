using Hify.Contracts.ModelProvider;

namespace Hify.Modules.Conversation.Features.Chat;

/// <summary>
/// 滑动窗口裁剪（纯函数，设计决策 上下文策略§2）：在给定 token 预算内，从最新历史往回保留消息，
/// 塞满即止；更早的丢弃。返回的列表保持原顺序（旧→新）。
/// 一期不做工具调用，历史只含 user/assistant，无需处理工具组不可拆边界（二期再加）。
/// </summary>
internal static class ContextWindowTrimmer
{
    /// <summary>
    /// 按预算裁剪历史消息。<paramref name="history"/> 须按旧→新排列；返回保留的子集（仍按旧→新）。
    /// </summary>
    /// <param name="history">历史消息（旧→新）。</param>
    /// <param name="budgetTokens">留给历史的 token 预算（&lt;=0 则全部丢弃）。</param>
    /// <param name="estimator">token 估算器。</param>
    public static IReadOnlyList<ChatMessage> Trim(
        IReadOnlyList<ChatMessage> history,
        int budgetTokens,
        ITokenEstimator estimator)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(estimator);

        if (budgetTokens <= 0 || history.Count == 0)
        {
            return [];
        }

        var kept = new List<ChatMessage>(history.Count);
        var used = 0;

        // 从最新一条往回累加，超出预算即停止（更早的全部丢弃）。
        for (var i = history.Count - 1; i >= 0; i--)
        {
            var cost = estimator.Estimate(history[i].Content);
            if (used + cost > budgetTokens)
            {
                break;
            }

            used += cost;
            kept.Add(history[i]);
        }

        kept.Reverse();
        return kept;
    }
}
