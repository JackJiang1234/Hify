namespace Hify.Modules.Knowledge.Domain;

/// <summary>文档处理状态取值（对应 document.status，varchar(32)）。</summary>
internal static class DocumentStatuses
{
    /// <summary>已上传，待处理（分块/嵌入）。</summary>
    public const string Pending = "pending";

    /// <summary>处理中（分块/嵌入进行）。</summary>
    public const string Processing = "processing";

    /// <summary>处理完成（分块与向量已就绪）。</summary>
    public const string Completed = "completed";

    /// <summary>处理失败。</summary>
    public const string Failed = "failed";
}
