using Hify.Shared.Results;

namespace Hify.Contracts.ModelProvider;

/// <summary>
/// ModelProvider 模块对外公开的只读查询能力，供 Agent/Conversation/Knowledge 等模块解析模型元数据。
/// 调用方不经此接口获取密钥；实际 LLM 调用走模块内适配器，凭证不出模块。
/// </summary>
public interface IModelProviderQuery
{
    /// <summary>按 Id 获取模型元数据（含能力位）。不存在返回 <c>NotFound</c>。</summary>
    /// <param name="modelId">模型 Id。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<ModelDto>> GetModelAsync(long modelId, CancellationToken cancellationToken);

    /// <summary>解析某供应商指定类型（chat/embedding）的默认模型。无默认返回 <c>NotFound</c>。</summary>
    /// <param name="providerId">供应商 Id。</param>
    /// <param name="modelType">模型类型，见 <see cref="ModelTypes"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<ModelDto>> GetDefaultModelAsync(long providerId, string modelType, CancellationToken cancellationToken);
}
