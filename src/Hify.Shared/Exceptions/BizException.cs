namespace Hify.Shared.Exceptions;

/// <summary>
/// 业务异常：承载 <see cref="Exceptions.ErrorCode"/>，由全局异常中间件捕获后转换为 Result 响应。
/// 主要用于错误传递时的领域包装（包裹底层异常）；可预期的业务失败优先直接返回 Result，不抛异常。
/// </summary>
public sealed class BizException : Exception
{
    /// <summary>以错误码构造，使用其默认提示信息。</summary>
    public BizException(ErrorCode errorCode)
        : this(errorCode, errorCode.GetMessage())
    {
    }

    /// <summary>以错误码构造并自定义提示信息（覆盖默认 message）。</summary>
    public BizException(ErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>包裹底层异常，使用错误码的默认提示信息。</summary>
    public BizException(ErrorCode errorCode, Exception innerException)
        : this(errorCode, errorCode.GetMessage(), innerException)
    {
    }

    /// <summary>包裹底层异常并自定义提示信息。</summary>
    public BizException(ErrorCode errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>业务错误码。</summary>
    public ErrorCode ErrorCode { get; }

    /// <summary>错误码对应的四位数值。</summary>
    public int Code => (int)ErrorCode;
}
