namespace Hify.Modules.Workflow.Features.Execution;

/// <summary>
/// 单次执行的可变状态：变量池（各节点输出）+ 触发输入 + 轨迹累积。仅在一次执行内使用、非线程安全。
/// （命名为 ExecutionState 以避开 BCL 的 <see cref="System.Threading.ExecutionContext"/>。）
/// </summary>
internal sealed class ExecutionState
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, object?>> _outputs =
        new(StringComparer.Ordinal);

    private readonly List<NodeTrace> _trace = [];

    /// <summary>构造。</summary>
    /// <param name="runInputs">工作流触发输入。</param>
    public ExecutionState(IReadOnlyDictionary<string, object?> runInputs)
    {
        ArgumentNullException.ThrowIfNull(runInputs);
        RunInputs = runInputs;
    }

    /// <summary>触发输入。</summary>
    public IReadOnlyDictionary<string, object?> RunInputs { get; }

    /// <summary>各节点输出视图（nodeId -&gt; 字段 -&gt; 值）。</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Outputs => _outputs;

    /// <summary>已累积的轨迹。</summary>
    public IReadOnlyList<NodeTrace> Trace => _trace;

    /// <summary>记录某节点输出。</summary>
    /// <param name="nodeId">节点 Id。</param>
    /// <param name="output">输出字段。</param>
    public void SetOutput(string nodeId, IReadOnlyDictionary<string, object?> output) => _outputs[nodeId] = output;

    /// <summary>追加一条节点轨迹。</summary>
    /// <param name="trace">轨迹。</param>
    public void AddTrace(NodeTrace trace) => _trace.Add(trace);
}
