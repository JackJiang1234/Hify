namespace Hify.Shared.Exceptions;

/// <summary>
/// 业务错误码。四位数字按模块分段，本枚举仅定义通用段（1000-1999）；
/// 各模块错误码（2000-2999 Provider … 7000-7999 Knowledge）在各自模块内定义。
/// 枚举值即对外返回的业务码，成功（200）由 Result.Ok 表达，不在此枚举内。
/// </summary>
public enum ErrorCode
{
    /// <summary>系统内部错误（未预期、不可恢复）。</summary>
    InternalError = 1000,

    /// <summary>参数错误 / 校验失败。</summary>
    ParamInvalid = 1001,

    /// <summary>未授权（未登录或凭证无效）。</summary>
    Unauthorized = 1002,

    /// <summary>无权限访问该资源。</summary>
    Forbidden = 1003,

    /// <summary>资源不存在。</summary>
    NotFound = 1004,

    /// <summary>资源或状态冲突。</summary>
    Conflict = 1005,

    /// <summary>请求过于频繁（限流）。</summary>
    TooManyRequests = 1006,

    /// <summary>操作超时。</summary>
    Timeout = 1007,
}
