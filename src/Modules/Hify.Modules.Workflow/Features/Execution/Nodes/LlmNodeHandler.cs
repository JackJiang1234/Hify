using Hify.Contracts.ModelProvider;
using Hify.Modules.Workflow.Domain;
using Hify.Shared.Results;

namespace Hify.Modules.Workflow.Features.Execution.Nodes;

/// <summary>
/// llm 节点：内联 modelId + prompt（不绑 Agent，决策 E）。解析 system/user 模板后经 <see cref="IModelInvoker"/>
/// 同步调用，输出字段 <c>text</c>。上游失败以 6004 返回。
/// </summary>
internal sealed class LlmNodeHandler : INodeHandler
{
    private const int DefaultMaxTokens = 1024;

    private readonly IModelInvoker _modelInvoker;
    private readonly VariableResolver _resolver;

    /// <summary>构造。</summary>
    /// <param name="modelInvoker">模型调用门面（ModelProvider 模块）。</param>
    /// <param name="resolver">变量解析器。</param>
    public LlmNodeHandler(IModelInvoker modelInvoker, VariableResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(modelInvoker);
        ArgumentNullException.ThrowIfNull(resolver);
        _modelInvoker = modelInvoker;
        _resolver = resolver;
    }

    /// <inheritdoc />
    public string NodeType => WorkflowNodeType.Llm;

    /// <inheritdoc />
    public async Task<Result<NodeResult>> ExecuteAsync(NodeRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var config = NodeConfigJson.Read<LlmConfig>(context.Node.Config);
        if (config.ModelId <= 0)
        {
            return Result<NodeResult>.Fail(
                (int)WorkflowErrorCode.ReferenceUnavailable,
                $"llm 节点 {context.Node.Id} 未配置有效 modelId。");
        }

        var messages = new List<ChatMessage>();
        var systemPrompt = _resolver.ResolveString(config.SystemPrompt, context.Outputs);
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new ChatMessage { Role = "system", Content = systemPrompt });
        }

        messages.Add(new ChatMessage
        {
            Role = "user",
            Content = _resolver.ResolveString(config.Prompt, context.Outputs),
        });

        var request = new ChatRequest
        {
            Messages = messages,
            MaxTokens = config.Params.MaxTokens > 0 ? config.Params.MaxTokens : DefaultMaxTokens,
            Temperature = config.Params.Temperature,
            TopP = config.Params.TopP,
        };

        var response = await _modelInvoker.ChatAsync(config.ModelId, request, cancellationToken)
            .ConfigureAwait(false);
        if (response.Code != 200 || response.Data is null)
        {
            return Result<NodeResult>.Fail(
                (int)WorkflowErrorCode.NodeExecutionFailed,
                $"llm 节点 {context.Node.Id} 调用失败：{response.Message}");
        }

        return Result<NodeResult>.Ok(NodeResult.Single(NodeOutputField.Text, response.Data.Content));
    }

    private sealed record LlmConfig
    {
        public long ModelId { get; init; }

        public string SystemPrompt { get; init; } = string.Empty;

        public string Prompt { get; init; } = string.Empty;

        public LlmParams Params { get; init; } = new();
    }

    private sealed record LlmParams
    {
        public int MaxTokens { get; init; }

        public double? Temperature { get; init; }

        public double? TopP { get; init; }
    }
}
