namespace Hify.Shared.Exceptions;

/// <summary>
/// <see cref="ErrorCode"/> 的辅助扩展：取数值码与默认提示信息。
/// </summary>
public static class ErrorCodeExtensions
{
    /// <summary>返回错误码的四位数值。</summary>
    public static int ToCode(this ErrorCode errorCode) => (int)errorCode;

    /// <summary>返回错误码对应的默认提示信息（不含敏感数据）。</summary>
    public static string GetMessage(this ErrorCode errorCode) => errorCode switch
    {
        ErrorCode.InternalError => "系统内部错误",
        ErrorCode.ParamInvalid => "参数错误",
        ErrorCode.Unauthorized => "未授权",
        ErrorCode.Forbidden => "无权限访问",
        ErrorCode.NotFound => "资源不存在",
        ErrorCode.Conflict => "资源冲突",
        ErrorCode.TooManyRequests => "请求过于频繁",
        ErrorCode.Timeout => "操作超时",
        _ => "未知错误",
    };
}
