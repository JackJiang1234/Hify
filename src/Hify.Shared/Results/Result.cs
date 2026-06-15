namespace Hify.Shared.Results;

/// <summary>
/// 统一响应包装。所有接口返回 <see cref="Result{T}"/>：<c>{ code, message, data }</c>。
/// </summary>
/// <typeparam name="T">业务数据类型。</typeparam>
public record Result<T>
{
    /// <summary>业务状态码。成功为 200；失败为四位模块错误码。</summary>
    public int Code { get; init; }

    /// <summary>提示信息。成功默认 <c>"success"</c>，失败为具体原因（不含敏感数据）。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>业务数据。失败或无数据时为 <c>null</c>。</summary>
    public T? Data { get; init; }

    /// <summary>构造成功响应（code=200）。</summary>
    /// <param name="data">业务数据。</param>
    /// <param name="message">提示信息，默认 <c>"success"</c>。</param>
    public static Result<T> Ok(T data, string message = "success") => new()
    {
        Code = 200,
        Message = message,
        Data = data,
    };

    /// <summary>构造失败响应。</summary>
    /// <param name="code">四位模块错误码。</param>
    /// <param name="message">失败原因。</param>
    public static Result<T> Fail(int code, string message) => new()
    {
        Code = code,
        Message = message,
        Data = default,
    };
}
