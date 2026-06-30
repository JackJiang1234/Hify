using Hify.Contracts.Mcp;
using Hify.Shared.Results;

namespace Hify.Modules.Workflow.Tests.Support;

/// <summary>可脚本化的假 MCP 工具门面：InvokeAsync 按注入委托返回；批量调用不支持（工作流一期不用）。</summary>
internal sealed class FakeMcpToolInvoker : IMcpToolInvoker
{
    private readonly Func<McpToolCall, Result<McpToolResult>> _invoke;

    public FakeMcpToolInvoker(Func<McpToolCall, Result<McpToolResult>> invoke) => _invoke = invoke;

    /// <summary>返回固定内容（非错误）的便捷构造。</summary>
    public static FakeMcpToolInvoker Returning(string content) =>
        new(_ => Result<McpToolResult>.Ok(new McpToolResult { Content = content, IsError = false }));

    public Task<Result<McpToolResult>> InvokeAsync(McpToolCall call, CancellationToken cancellationToken) =>
        Task.FromResult(_invoke(call));

    public Task<IReadOnlyList<McpToolInvocation>> InvokeManyAsync(
        IReadOnlyList<McpToolCall> calls, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
