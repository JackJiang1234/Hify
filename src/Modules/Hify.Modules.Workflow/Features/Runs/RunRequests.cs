namespace Hify.Modules.Workflow.Features.Runs;

/// <summary>试运行请求。<see cref="Inputs"/> 为 start 节点声明输入的字符串值（名 -&gt; 值）。</summary>
internal sealed record CreateRunRequest
{
    /// <summary>触发输入（输入名 -&gt; 字符串值）。可空缺，required 校验在 start 节点执行时。</summary>
    public IReadOnlyDictionary<string, string> Inputs { get; init; } = new Dictionary<string, string>();
}
