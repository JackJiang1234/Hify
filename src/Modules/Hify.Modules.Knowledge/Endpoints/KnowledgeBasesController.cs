using Hify.Contracts.Knowledge;
using Hify.Modules.Knowledge.Features.KnowledgeBases;
using Hify.Modules.Knowledge.Features.Search;
using Hify.Shared.Results;

using Microsoft.AspNetCore.Mvc;

namespace Hify.Modules.Knowledge.Endpoints;

/// <summary>知识库管理接口。统一返回 <see cref="Result{T}"/>；入参校验由全局过滤器执行。</summary>
[ApiController]
[Route("api/v1/knowledge-bases")]
internal sealed class KnowledgeBasesController : ControllerBase
{
    private readonly KnowledgeBaseService _service;
    private readonly IKnowledgeQuery _search;

    public KnowledgeBasesController(KnowledgeBaseService service, IKnowledgeQuery search)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(search);
        _service = service;
        _search = search;
    }

    /// <summary>创建知识库（校验嵌入模型为 1536 维 embedding）。</summary>
    [HttpPost]
    public Task<Result<KnowledgeBaseDto>> Create([FromBody] CreateKnowledgeBaseRequest request) =>
        _service.CreateAsync(request, HttpContext.RequestAborted);

    /// <summary>知识库详情。</summary>
    [HttpGet("{id:long}")]
    public Task<Result<KnowledgeBaseDto>> Get(long id) =>
        _service.GetAsync(id, HttpContext.RequestAborted);

    /// <summary>分页列出知识库。</summary>
    [HttpGet]
    public Task<PageResult<KnowledgeBaseDto>> List([FromQuery] int page = 1, [FromQuery] int size = 20) =>
        _service.ListAsync(page, size, HttpContext.RequestAborted);

    /// <summary>更新知识库（库内已有分块时嵌入模型/分块参数不可改）。</summary>
    [HttpPut("{id:long}")]
    public Task<Result<KnowledgeBaseDto>> Update(long id, [FromBody] UpdateKnowledgeBaseRequest request) =>
        _service.UpdateAsync(id, request, HttpContext.RequestAborted);

    /// <summary>删除知识库（级联软删文档与分块）。</summary>
    [HttpDelete("{id:long}")]
    public Task<Result<bool>> Delete(long id) =>
        _service.DeleteAsync(id, HttpContext.RequestAborted);

    /// <summary>单库检索预览（管理员调参用）。</summary>
    [HttpPost("{id:long}/search")]
    public Task<Result<IReadOnlyList<KnowledgeChunkDto>>> Search(long id, [FromBody] KnowledgeBaseSearchRequest request) =>
        _search.SearchAsync(
            new KnowledgeSearchRequest
            {
                KnowledgeBaseIds = [id],
                Query = request.Query,
                TopK = request.TopK,
                ScoreThreshold = request.ScoreThreshold,
            },
            HttpContext.RequestAborted);
}
