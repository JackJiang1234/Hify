using Hify.Contracts.Mcp;
using Hify.Shared.Results;

namespace Hify.Modules.Conversation.Tests.Support;

/// <summary>
/// <see cref="IMcpToolInvoker"/> 的内存替身：记录收到的调用，按注入的 responder 返回结果（默认成功回显 toolId）。
/// </summary>
internal sealed class FakeMcpToolInvoker : IMcpToolInvoker
{
    private readonly Func<McpToolCall, Result<McpToolResult>> _responder;

    public FakeMcpToolInvoker(Func<McpToolCall, Result<McpToolResult>>? responder = null) =>
        _responder = responder ?? (call => Result<McpToolResult>.Ok(new McpToolResult { Content = $"result:{call.ToolId}" }));

    /// <summary>按调用顺序记录所有收到的工具调用，供断言。</summary>
    public List<McpToolCall> Received { get; } = [];

    public Task<Result<McpToolResult>> InvokeAsync(McpToolCall call, CancellationToken cancellationToken)
    {
        Received.Add(call);
        return Task.FromResult(_responder(call));
    }

    public Task<IReadOnlyList<McpToolInvocation>> InvokeManyAsync(IReadOnlyList<McpToolCall> calls, CancellationToken cancellationToken)
    {
        IReadOnlyList<McpToolInvocation> results = calls
            .Select(call =>
            {
                Received.Add(call);
                return new McpToolInvocation { CallId = call.CallId, ToolId = call.ToolId, Result = _responder(call) };
            })
            .ToList();
        return Task.FromResult(results);
    }
}
