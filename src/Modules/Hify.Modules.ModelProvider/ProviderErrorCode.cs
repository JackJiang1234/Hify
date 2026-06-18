namespace Hify.Modules.ModelProvider;

/// <summary>
/// ModelProvider 模块错误码（2xxx 段）。枚举值即对外返回的四位业务码。
/// 适配器调用相关码在此；CRUD 相关码（不存在/冲突等）在 P5 功能切片补充。
/// </summary>
internal enum ProviderErrorCode
{
    /// <summary>供应商不可达（网络错误 / 超时）。</summary>
    ProviderUnreachable = 2001,

    /// <summary>供应商鉴权失败（401/403）。</summary>
    ProviderAuthFailed = 2002,

    /// <summary>供应商限流（429）。</summary>
    ProviderRateLimited = 2003,

    /// <summary>供应商响应无法解析。</summary>
    ProviderResponseInvalid = 2004,

    /// <summary>供应商调用失败（其它非成功状态）。</summary>
    ProviderCallFailed = 2005,

    /// <summary>该供应商不支持嵌入。</summary>
    EmbeddingNotSupported = 2006,

    /// <summary>供应商不存在。</summary>
    ProviderNotFound = 2007,

    /// <summary>供应商名称冲突。</summary>
    ProviderNameConflict = 2008,

    /// <summary>模型不存在。</summary>
    ModelNotFound = 2009,

    /// <summary>供应商已停用。</summary>
    ProviderDisabled = 2010,

    /// <summary>模型已停用。</summary>
    ModelDisabled = 2011,

    /// <summary>密钥解密失败。</summary>
    CredentialError = 2012,

    /// <summary>同一供应商下模型名冲突。</summary>
    ModelNameConflict = 2013,
}
