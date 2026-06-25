using FluentValidation;

namespace Hify.Modules.Knowledge.Features.Documents;

/// <summary>上传文档请求。<see cref="Content"/> 为解码后的文本（一期仅 TXT）；文件类型与库存在性在服务层校验。</summary>
internal sealed record UploadDocumentRequest
{
    /// <summary>目标知识库 Id。</summary>
    public long KnowledgeBaseId { get; init; }

    /// <summary>文件名（含扩展名，用于判定类型与展示）。</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>文档文本内容。</summary>
    public string Content { get; init; } = string.Empty;
}

/// <summary>上传请求的校验上下界。</summary>
internal static class DocumentValidation
{
    /// <summary>文件名最大长度。</summary>
    public const int MaxFileNameLength = 256;

    /// <summary>单文档最大字符数（约 1M，防止超大文件拖垮处理流水线）。</summary>
    public const int MaxContentLength = 1_000_000;
}

/// <summary>上传请求校验。</summary>
internal sealed class UploadDocumentRequestValidator : AbstractValidator<UploadDocumentRequest>
{
    public UploadDocumentRequestValidator()
    {
        RuleFor(request => request.KnowledgeBaseId).GreaterThan(0).WithMessage("knowledgeBaseId 非法");
        RuleFor(request => request.FileName).NotEmpty().WithMessage("fileName 不能为空")
            .MaximumLength(DocumentValidation.MaxFileNameLength).WithMessage($"fileName 不超过 {DocumentValidation.MaxFileNameLength} 字符");
        RuleFor(request => request.Content).NotEmpty().WithMessage("content 不能为空")
            .MaximumLength(DocumentValidation.MaxContentLength).WithMessage($"content 不超过 {DocumentValidation.MaxContentLength} 字符");
    }
}
