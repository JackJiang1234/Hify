namespace Hify.Modules.Conversation.Features.Chat;

/// <summary>
/// Token 估算（一期粗估，设计决策 B）。仅用于上下文裁剪的预算计算，不追求精确，
/// 偏保守（宁可高估，留余量）。二期可换精确 tokenizer 而不改调用方。
/// </summary>
internal interface ITokenEstimator
{
    /// <summary>估算一段文本的 token 数。</summary>
    /// <param name="text">文本。</param>
    int Estimate(string text);
}
