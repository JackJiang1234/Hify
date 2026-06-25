using Hify.Modules.Knowledge.Features.Documents;

namespace Hify.Modules.Knowledge.Tests.Features.Documents;

/// <summary>上传请求的格式与范围校验（无需 DB）。文件类型为 txt 与库存在性在服务层校验。</summary>
public sealed class UploadDocumentRequestValidatorTests
{
    private static readonly UploadDocumentRequestValidator Validator = new();

    private static UploadDocumentRequest Valid() => new()
    {
        KnowledgeBaseId = 1,
        FileName = "manual.txt",
        Content = "一些文档内容",
    };

    [Fact]
    public void Valid_Passes() => Assert.True(Validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void KnowledgeBaseId_MustBePositive(long id, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { KnowledgeBaseId = id }).IsValid);

    [Theory]
    [InlineData("", false)]
    [InlineData("a.txt", true)]
    public void FileName_Required(string fileName, bool expected) =>
        Assert.Equal(expected, Validator.Validate(Valid() with { FileName = fileName }).IsValid);

    [Fact]
    public void Content_Empty_Fails() =>
        Assert.False(Validator.Validate(Valid() with { Content = "" }).IsValid);

    [Fact]
    public void Content_TooLong_Fails() =>
        Assert.False(Validator.Validate(Valid() with { Content = new string('x', DocumentValidation.MaxContentLength + 1) }).IsValid);
}
