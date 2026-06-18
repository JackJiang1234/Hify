namespace Hify.Modules.ModelProvider.Security;

/// <summary>从明文密钥生成脱敏提示（仅末位），供 UI 展示；绝不暴露完整密钥。</summary>
internal static class ApiKeyHint
{
    private const int VisibleTailLength = 4;

    /// <summary>
    /// 生成提示：长度 &gt; 4 时取末 4 位并以 <c>…</c> 前缀（如 <c>…a1b2</c>）；长度 ≤ 4 全部掩码为 <c>…</c>；空串返回空串。
    /// </summary>
    /// <param name="apiKey">明文密钥。</param>
    public static string Of(string apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        if (apiKey.Length == 0)
        {
            return string.Empty;
        }

        return apiKey.Length <= VisibleTailLength
            ? "…"
            : $"…{apiKey[^VisibleTailLength..]}";
    }
}
