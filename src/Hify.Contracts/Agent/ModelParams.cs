namespace Hify.Contracts.Agent;

/// <summary>
/// Agent 的模型生成参数（落库为 jsonb）。各字段可空：为空表示沿用模型自身默认值，不强制覆盖。
/// </summary>
public record ModelParams
{
    /// <summary>采样温度，取值 <c>[0.0, 2.0]</c>；为空用模型默认。</summary>
    public double? Temperature { get; init; }

    /// <summary>核采样 top-p，取值 <c>[0.0, 1.0]</c>；为空用模型默认。</summary>
    public double? TopP { get; init; }

    /// <summary>单次生成最大 token 数，须 <c>&gt; 0</c> 且不超过模型 <c>MaxOutputTokens</c>；为空用模型默认。</summary>
    public int? MaxTokens { get; init; }
}
