using System.Text;

using Hify.Modules.Knowledge.Features.Documents;
using Hify.Shared.Exceptions;
using Hify.Shared.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hify.Modules.Knowledge.Endpoints;

/// <summary>
/// 文档管理接口（嵌套于知识库下）。上传为 multipart/form-data：控制器按 UTF-8 解码文件为文本后交服务处理。
/// 统一返回 <see cref="Result{T}"/>。
/// </summary>
[ApiController]
[Route("api/v1/knowledge-bases/{kbId:long}/documents")]
internal sealed class DocumentsController : ControllerBase
{
    // 上传体积上限：约 5MB，配合服务侧字符数与去重校验，避免超大文件占内存。
    private const long MaxUploadBytes = 5L * 1024 * 1024;

    private readonly DocumentService _service;

    public DocumentsController(DocumentService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>上传 TXT 文档并同步分块、嵌入入库。</summary>
    [HttpPost]
    public async Task<Result<DocumentDto>> Upload(long kbId, IFormFile? file)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (file is null || file.Length == 0)
        {
            return Result<DocumentDto>.Fail((int)ErrorCode.ParamInvalid, "未提供文件或文件为空。");
        }

        if (file.Length > MaxUploadBytes)
        {
            return Result<DocumentDto>.Fail((int)ErrorCode.ParamInvalid, $"文件超过大小上限 {MaxUploadBytes / (1024 * 1024)}MB。");
        }

        string content;
        await using (var stream = file.OpenReadStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            content = await reader.ReadToEndAsync(cancellationToken);
        }

        return await _service.UploadAsync(
            new UploadDocumentRequest { KnowledgeBaseId = kbId, FileName = file.FileName, Content = content },
            cancellationToken);
    }

    /// <summary>分页列出知识库内文档。</summary>
    [HttpGet]
    public Task<PageResult<DocumentDto>> List(long kbId, [FromQuery] int page = 1, [FromQuery] int size = 20) =>
        _service.ListAsync(kbId, page, size, HttpContext.RequestAborted);

    /// <summary>文档详情。</summary>
    [HttpGet("{docId:long}")]
    public Task<Result<DocumentDto>> Get(long kbId, long docId) =>
        _service.GetAsync(kbId, docId, HttpContext.RequestAborted);

    /// <summary>删除文档（级联软删其分块）。</summary>
    [HttpDelete("{docId:long}")]
    public Task<Result<bool>> Delete(long kbId, long docId) =>
        _service.DeleteAsync(kbId, docId, HttpContext.RequestAborted);
}
