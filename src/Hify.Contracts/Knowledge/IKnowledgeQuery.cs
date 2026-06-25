using Hify.Shared.Results;

namespace Hify.Contracts.Knowledge;

/// <summary>
/// Knowledge 模块对外公开的只读检索能力，供对话引擎（Conversation，L2）做 RAG。
/// 传入 query 文本，内部按各知识库绑定的嵌入模型向量化并检索 pgvector，调用方无需感知嵌入细节。
/// </summary>
public interface IKnowledgeQuery
{
    /// <summary>
    /// 跨指定知识库做相似度检索，按相似度倒序返回不超过 TopK 个分块。
    /// 不存在/已停用的库被跳过；全部无命中或无绑定返回空列表（成功，data 为 []）。
    /// embedding 调用失败等返回失败 <see cref="Result{T}"/>（7xxx），由调用方决定是否降级。
    /// </summary>
    /// <param name="request">检索请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<IReadOnlyList<KnowledgeChunkDto>>> SearchAsync(
        KnowledgeSearchRequest request,
        CancellationToken cancellationToken);
}
