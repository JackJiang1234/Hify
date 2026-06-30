using Hify.Contracts.Mcp;
using Hify.Shared.Results;

namespace Hify.Modules.Conversation.Tests.Support;

/// <summary>
/// <see cref="IMcpToolQuery"/> 的内存替身：按 Id 返回预置工具元数据（默认空，对话引擎走纯文本路径）。
/// </summary>
internal sealed class FakeMcpToolQuery : IMcpToolQuery
{
    private readonly IReadOnlyList<McpToolDto> _tools;

    public FakeMcpToolQuery(params McpToolDto[] tools) => _tools = tools;

    public static McpToolDto Tool(long id, string name, string inputSchema = "{}") => new()
    {
        Id = id,
        Name = name,
        Description = $"{name} desc",
        InputSchema = inputSchema,
        Available = true,
        Enabled = true,
    };

    public Task<Result<IReadOnlyList<McpToolDto>>> GetInvocableToolsAsync(IReadOnlyList<long> toolIds, CancellationToken cancellationToken)
    {
        IReadOnlyList<McpToolDto> matched = _tools.Where(tool => toolIds.Contains(tool.Id)).ToList();
        return Task.FromResult(Result<IReadOnlyList<McpToolDto>>.Ok(matched));
    }
}
