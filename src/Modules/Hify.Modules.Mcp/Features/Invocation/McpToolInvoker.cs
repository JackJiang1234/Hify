using System.Security.Cryptography;

using Hify.Contracts.Mcp;
using Hify.Modules.Mcp.Persistence;
using Hify.Modules.Mcp.Protocol;
using Hify.Shared.Results;
using Hify.Shared.Security;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;

namespace Hify.Modules.Mcp.Features.Invocation;

/// <summary>
/// <see cref="IMcpToolInvoker"/> 实现。三层并发控制：每-Server 舱壁+熔断（管道）、单批并行度上限（信号量）、
/// 每调用超时（链路取消）。批量调用逐项隔离失败、结果顺序与入参一致。
/// 注意：解析（读库+解密）顺序进行（DbContext 非线程安全），仅网络调用阶段并发。
/// </summary>
internal sealed class McpToolInvoker : IMcpToolInvoker
{
    private readonly McpDbContext _db;
    private readonly IMcpProtocolClient _protocolClient;
    private readonly ICredentialProtector _protector;
    private readonly McpResiliencePipelineProvider _pipelineProvider;
    private readonly McpOptions _options;

    public McpToolInvoker(
        McpDbContext db,
        IMcpProtocolClient protocolClient,
        ICredentialProtector protector,
        McpResiliencePipelineProvider pipelineProvider,
        IOptions<McpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(protocolClient);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(pipelineProvider);
        ArgumentNullException.ThrowIfNull(options);
        _db = db;
        _protocolClient = protocolClient;
        _protector = protector;
        _pipelineProvider = pipelineProvider;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<Result<McpToolResult>> InvokeAsync(McpToolCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);

        var resolved = await ResolveAsync(call, cancellationToken);
        return resolved.Code != 200
            ? Result<McpToolResult>.Fail(resolved.Code, resolved.Message)
            : await ExecuteCallAsync(resolved.Data!, call.ArgumentsJson, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<McpToolInvocation>> InvokeManyAsync(
        IReadOnlyList<McpToolCall> calls, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calls);

        if (calls.Count == 0)
        {
            return [];
        }

        // 1. 顺序解析（共享 DbContext 不可并发）。
        var resolutions = new List<(McpToolCall Call, Result<ResolvedCall> Resolved)>(calls.Count);
        foreach (var call in calls)
        {
            resolutions.Add((call, await ResolveAsync(call, cancellationToken)));
        }

        // 2. 并发执行（不碰 DbContext），单批并行度由信号量限制；结果顺序与入参一致。
        using var gate = new SemaphoreSlim(_options.MaxParallelToolCalls);
        var tasks = resolutions.Select(item => ExecuteResolvedAsync(item.Call, item.Resolved, gate, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    private async Task<McpToolInvocation> ExecuteResolvedAsync(
        McpToolCall call, Result<ResolvedCall> resolved, SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        if (resolved.Code != 200)
        {
            return Invocation(call, Result<McpToolResult>.Fail(resolved.Code, resolved.Message));
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            return Invocation(call, await ExecuteCallAsync(resolved.Data!, call.ArgumentsJson, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 单项异常隔离，不掀翻整批；整体取消则向上冒泡。
            return Invocation(call, Result<McpToolResult>.Fail((int)McpErrorCode.McpToolCallFailed, ex.Message));
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<Result<McpToolResult>> ExecuteCallAsync(ResolvedCall resolved, string argumentsJson, CancellationToken cancellationToken)
    {
        var pipeline = _pipelineProvider.GetPipeline(resolved.ServerId);
        var timeoutMs = resolved.TimeoutMs > 0 ? resolved.TimeoutMs : _options.CallTimeoutSeconds * 1000;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs);

        try
        {
            return await pipeline.ExecuteAsync(
                async token =>
                {
                    Result<McpToolResult> result;
                    try
                    {
                        result = await _protocolClient.CallToolAsync(resolved.Connection, resolved.ToolName, argumentsJson, token);
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        // 内部超时：转成异常让熔断/舱壁计入失败（协议客户端本会把它当取消重抛）。
                        throw new McpTransportFailureException((int)McpErrorCode.McpToolCallTimeout, "工具调用超时。");
                    }

                    // 传输/服务端级失败重抛，让熔断器看到（工具级 isError 是成功 Result，不在此列）。
                    if (result.Code != 200)
                    {
                        throw new McpTransportFailureException(result.Code, result.Message);
                    }

                    return result;
                },
                timeoutCts.Token);
        }
        catch (McpTransportFailureException ex)
        {
            return Result<McpToolResult>.Fail(ex.Code, ex.Message);
        }
        catch (BrokenCircuitException)
        {
            return Result<McpToolResult>.Fail((int)McpErrorCode.McpServerUnreachable, "MCP Server 暂时不可用（熔断中）。");
        }
        catch (RateLimiterRejectedException)
        {
            return Result<McpToolResult>.Fail((int)McpErrorCode.McpToolCallFailed, "MCP Server 并发已满，请稍后重试。");
        }
    }

    private async Task<Result<ResolvedCall>> ResolveAsync(McpToolCall call, CancellationToken cancellationToken)
    {
        var tool = await _db.McpTools.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == call.ToolId, cancellationToken);
        if (tool is null)
        {
            return Result<ResolvedCall>.Fail((int)McpErrorCode.McpToolNotFound, "工具不存在。");
        }

        if (!tool.Enabled || !tool.Available)
        {
            return Result<ResolvedCall>.Fail((int)McpErrorCode.McpToolUnavailable, "工具不可调用（已停用或服务端已移除）。");
        }

        var server = await _db.McpServers.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == tool.ServerId, cancellationToken);
        if (server is null)
        {
            return Result<ResolvedCall>.Fail((int)McpErrorCode.McpServerNotFound, "MCP Server 不存在。");
        }

        if (!server.Enabled)
        {
            return Result<ResolvedCall>.Fail((int)McpErrorCode.McpServerDisabled, "MCP Server 已停用。");
        }

        string apiKey;
        try
        {
            apiKey = _protector.Unprotect(server.ApiKeyCipher);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return Result<ResolvedCall>.Fail((int)McpErrorCode.CredentialError, "凭证解密失败。");
        }

        var connection = new McpServerConnection
        {
            Endpoint = server.Endpoint,
            AuthType = server.AuthType,
            AuthHeaderName = server.AuthHeaderName,
            ApiKey = apiKey,
        };
        return Result<ResolvedCall>.Ok(new ResolvedCall(connection, server.Id, tool.Name, server.TimeoutMs));
    }

    private static McpToolInvocation Invocation(McpToolCall call, Result<McpToolResult> result) => new()
    {
        CallId = call.CallId,
        ToolId = call.ToolId,
        Result = result,
    };

    /// <summary>已解析、可直接发起网络调用的请求（不再依赖 DbContext）。</summary>
    private sealed record ResolvedCall(McpServerConnection Connection, long ServerId, string ToolName, int TimeoutMs);

    /// <summary>把传输/服务端级失败穿过 Polly 管道，使熔断/舱壁计入；管道外捕获后转回 Result。</summary>
    private sealed class McpTransportFailureException : Exception
    {
        public McpTransportFailureException(int code, string message)
            : base(message)
        {
            Code = code;
        }

        public int Code { get; }
    }
}
