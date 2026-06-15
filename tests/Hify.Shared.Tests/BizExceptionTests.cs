using Hify.Shared.Exceptions;

namespace Hify.Shared.Tests;

public class BizExceptionTests
{
    [Theory]
    [InlineData(ErrorCode.ParamInvalid)]
    [InlineData(ErrorCode.NotFound)]
    [InlineData(ErrorCode.InternalError)]
    public void Ctor_WithErrorCode_UsesDefaultMessage(ErrorCode code)
    {
        var ex = new BizException(code);

        Assert.Equal(code, ex.ErrorCode);
        Assert.Equal(code.ToCode(), ex.Code);
        Assert.Equal(code.GetMessage(), ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Theory]
    [InlineData(ErrorCode.ParamInvalid, "字段 name 不能为空")]
    [InlineData(ErrorCode.Conflict, "providerName 已存在")]
    public void Ctor_WithCustomMessage_OverridesDefault(ErrorCode code, string message)
    {
        var ex = new BizException(code, message);

        Assert.Equal(code, ex.ErrorCode);
        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void Ctor_WithInnerException_WrapsItAndUsesDefaultMessage()
    {
        var inner = new InvalidOperationException("boom");

        var ex = new BizException(ErrorCode.InternalError, inner);

        Assert.Same(inner, ex.InnerException);
        Assert.Equal(ErrorCode.InternalError.GetMessage(), ex.Message);
    }

    [Fact]
    public void Ctor_WithCustomMessageAndInner_SetsBoth()
    {
        var inner = new TimeoutException();

        var ex = new BizException(ErrorCode.Timeout, "调用下游超时", inner);

        Assert.Equal("调用下游超时", ex.Message);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal(ErrorCode.Timeout, ex.ErrorCode);
    }
}
