namespace Hify.Modules.Knowledge;

/// <summary>
/// Knowledge 模块错误码（7xxx 段）。枚举值即对外返回的四位业务码。
/// 格式/范围校验失败由全局校验过滤器统一返回通用码 1001，不在此枚举内。
/// </summary>
internal enum KnowledgeErrorCode
{
    /// <summary>知识库不存在。</summary>
    KnowledgeBaseNotFound = 7001,

    /// <summary>文档不存在。</summary>
    DocumentNotFound = 7002,

    /// <summary>嵌入模型维度不符（固定要求 1536）。</summary>
    EmbeddingModelDimensionMismatch = 7003,

    /// <summary>知识库配置已冻结（库内已有嵌入分块，不可改嵌入模型 / 分块参数）。</summary>
    KnowledgeBaseConfigLocked = 7004,

    /// <summary>嵌入调用失败（检索 / 入库时）。</summary>
    EmbeddingFailed = 7005,

    /// <summary>文档处理失败（分块 / 嵌入流水线）。</summary>
    DocumentProcessingFailed = 7006,

    /// <summary>不支持的文件类型（一期仅 TXT）。</summary>
    UnsupportedFileType = 7007,

    /// <summary>引用的嵌入模型非法（不存在 / 非 embedding 类型 / 已停用）。</summary>
    EmbeddingModelInvalid = 7008,

    /// <summary>知识库名称冲突。</summary>
    KnowledgeBaseNameConflict = 7009,

    /// <summary>同一知识库内已存在相同内容的文档（content_hash 去重）。</summary>
    DocumentContentDuplicate = 7010,
}
