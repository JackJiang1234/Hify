using System.Text.Json;

using FluentValidation;

using Hify.Contracts.ModelProvider;

namespace Hify.Modules.ModelProvider.Features.Providers;

/// <summary>创建供应商请求。<see cref="ApiKey"/> 为明文，落库前加密。</summary>
internal sealed record CreateProviderRequest
{
    /// <summary>名称（唯一）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>供应商类型，见 <see cref="ProviderTypes"/>。</summary>
    public string ProviderType { get; init; } = string.Empty;

    /// <summary>API 基址。</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>鉴权方式，见 <see cref="AuthTypes"/>。</summary>
    public string AuthType { get; init; } = AuthTypes.None;

    /// <summary><c>header</c> 鉴权下的头名。</summary>
    public string AuthHeaderName { get; init; } = string.Empty;

    /// <summary>明文密钥（可空，none 鉴权时留空）。</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>私有静态配置（JSON 头映射）。</summary>
    public string Settings { get; init; } = "{}";

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>更新供应商请求。<see cref="ApiKey"/> 留空表示不改动既有密钥。</summary>
internal sealed record UpdateProviderRequest
{
    /// <summary>名称（唯一）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>供应商类型，见 <see cref="ProviderTypes"/>。</summary>
    public string ProviderType { get; init; } = string.Empty;

    /// <summary>API 基址。</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>鉴权方式，见 <see cref="AuthTypes"/>。</summary>
    public string AuthType { get; init; } = AuthTypes.None;

    /// <summary><c>header</c> 鉴权下的头名。</summary>
    public string AuthHeaderName { get; init; } = string.Empty;

    /// <summary>明文密钥；留空保留原密钥。</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>私有静态配置（JSON 头映射）。</summary>
    public string Settings { get; init; } = "{}";

    /// <summary>是否启用。</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>创建请求校验。</summary>
internal sealed class CreateProviderRequestValidator : AbstractValidator<CreateProviderRequest>
{
    public CreateProviderRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage("name 不能为空").MaximumLength(128).WithMessage("name 不超过 128 字符");
        RuleFor(request => request.ProviderType).Must(ProviderValidation.BeKnownProviderType).WithMessage("providerType 非法（openai | claude | ollama）");
        RuleFor(request => request.BaseUrl).NotEmpty().WithMessage("baseUrl 不能为空").MaximumLength(512).WithMessage("baseUrl 不超过 512 字符");
        RuleFor(request => request.AuthType).Must(ProviderValidation.BeKnownAuthType).WithMessage("authType 非法（none | bearer | header）");
        RuleFor(request => request.AuthHeaderName).NotEmpty().When(request => request.AuthType == AuthTypes.Header).WithMessage("header 鉴权需提供 authHeaderName");
        RuleFor(request => request.Settings).Must(ProviderValidation.BeValidJsonObject).WithMessage("settings 须为 JSON 对象");
    }
}

/// <summary>更新请求校验。</summary>
internal sealed class UpdateProviderRequestValidator : AbstractValidator<UpdateProviderRequest>
{
    public UpdateProviderRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage("name 不能为空").MaximumLength(128).WithMessage("name 不超过 128 字符");
        RuleFor(request => request.ProviderType).Must(ProviderValidation.BeKnownProviderType).WithMessage("providerType 非法（openai | claude | ollama）");
        RuleFor(request => request.BaseUrl).NotEmpty().WithMessage("baseUrl 不能为空").MaximumLength(512).WithMessage("baseUrl 不超过 512 字符");
        RuleFor(request => request.AuthType).Must(ProviderValidation.BeKnownAuthType).WithMessage("authType 非法（none | bearer | header）");
        RuleFor(request => request.AuthHeaderName).NotEmpty().When(request => request.AuthType == AuthTypes.Header).WithMessage("header 鉴权需提供 authHeaderName");
        RuleFor(request => request.Settings).Must(ProviderValidation.BeValidJsonObject).WithMessage("settings 须为 JSON 对象");
    }
}

/// <summary>供应商请求的共用校验谓词。</summary>
internal static class ProviderValidation
{
    private static readonly string[] KnownProviderTypes = [ProviderTypes.OpenAi, ProviderTypes.Claude, ProviderTypes.Ollama];
    private static readonly string[] KnownAuthTypes = [AuthTypes.None, AuthTypes.Bearer, AuthTypes.Header];

    public static bool BeKnownProviderType(string value) => Array.IndexOf(KnownProviderTypes, value) >= 0;

    public static bool BeKnownAuthType(string value) => Array.IndexOf(KnownAuthTypes, value) >= 0;

    public static bool BeValidJsonObject(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
