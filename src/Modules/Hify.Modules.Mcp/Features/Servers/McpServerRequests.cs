using FluentValidation;

using Hify.Contracts.ModelProvider;

namespace Hify.Modules.Mcp.Features.Servers;

/// <summary>创建 MCP Server 请求。<see cref="ApiKey"/> 为明文，落库前加密。传输固定 streamable_http，不在请求内。</summary>
internal sealed record CreateMcpServerRequest
{
    /// <summary>名称（唯一）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Streamable HTTP 端点 URL。</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>鉴权方式，见 <see cref="AuthTypes"/>。</summary>
    public string AuthType { get; init; } = AuthTypes.None;

    /// <summary><c>header</c> 鉴权下的头名。</summary>
    public string AuthHeaderName { get; init; } = string.Empty;

    /// <summary>明文凭证（可空，none 鉴权时留空）。</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>调用超时（毫秒），0=用全局默认。</summary>
    public int TimeoutMs { get; init; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>更新 MCP Server 请求。<see cref="ApiKey"/> 留空表示不改动既有凭证。</summary>
internal sealed record UpdateMcpServerRequest
{
    /// <summary>名称（唯一）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Streamable HTTP 端点 URL。</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>鉴权方式，见 <see cref="AuthTypes"/>。</summary>
    public string AuthType { get; init; } = AuthTypes.None;

    /// <summary><c>header</c> 鉴权下的头名。</summary>
    public string AuthHeaderName { get; init; } = string.Empty;

    /// <summary>明文凭证；留空保留原凭证。</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>调用超时（毫秒），0=用全局默认。</summary>
    public int TimeoutMs { get; init; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>创建请求校验。</summary>
internal sealed class CreateMcpServerRequestValidator : AbstractValidator<CreateMcpServerRequest>
{
    public CreateMcpServerRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage("name 不能为空").MaximumLength(128).WithMessage("name 不超过 128 字符");
        RuleFor(request => request.Endpoint).NotEmpty().WithMessage("endpoint 不能为空").MaximumLength(512).WithMessage("endpoint 不超过 512 字符")
            .Must(McpServerValidation.BeHttpUrl).WithMessage("endpoint 须为 http/https 绝对 URL");
        RuleFor(request => request.AuthType).Must(McpServerValidation.BeKnownAuthType).WithMessage("authType 非法（none | bearer | header）");
        RuleFor(request => request.AuthHeaderName).NotEmpty().When(request => request.AuthType == AuthTypes.Header).WithMessage("header 鉴权需提供 authHeaderName");
        RuleFor(request => request.TimeoutMs).GreaterThanOrEqualTo(0).WithMessage("timeoutMs 不能为负");
    }
}

/// <summary>更新请求校验。</summary>
internal sealed class UpdateMcpServerRequestValidator : AbstractValidator<UpdateMcpServerRequest>
{
    public UpdateMcpServerRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage("name 不能为空").MaximumLength(128).WithMessage("name 不超过 128 字符");
        RuleFor(request => request.Endpoint).NotEmpty().WithMessage("endpoint 不能为空").MaximumLength(512).WithMessage("endpoint 不超过 512 字符")
            .Must(McpServerValidation.BeHttpUrl).WithMessage("endpoint 须为 http/https 绝对 URL");
        RuleFor(request => request.AuthType).Must(McpServerValidation.BeKnownAuthType).WithMessage("authType 非法（none | bearer | header）");
        RuleFor(request => request.AuthHeaderName).NotEmpty().When(request => request.AuthType == AuthTypes.Header).WithMessage("header 鉴权需提供 authHeaderName");
        RuleFor(request => request.TimeoutMs).GreaterThanOrEqualTo(0).WithMessage("timeoutMs 不能为负");
    }
}

/// <summary>MCP Server 请求的共用校验谓词。</summary>
internal static class McpServerValidation
{
    private static readonly string[] KnownAuthTypes = [AuthTypes.None, AuthTypes.Bearer, AuthTypes.Header];

    public static bool BeKnownAuthType(string value) => Array.IndexOf(KnownAuthTypes, value) >= 0;

    public static bool BeHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
