using System.Security.Cryptography;

using Hify.Contracts.ModelProvider;
using Hify.Modules.ModelProvider.Adapters;
using Hify.Modules.ModelProvider.Persistence;
using Hify.Modules.ModelProvider.Security;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.ModelProvider.Invocation;

/// <summary>
/// <see cref="IModelInvoker"/> 实现：modelId → 解析模型与供应商 → 解密密钥 → 选适配器 → 调用。
/// 解密所得明文仅在内存短暂存在，绝不入日志，也不出模块。
/// </summary>
internal sealed class ModelInvoker : IModelInvoker
{
    private readonly ModelProviderDbContext _db;
    private readonly ICredentialProtector _protector;
    private readonly IModelProviderAdapterFactory _adapterFactory;

    public ModelInvoker(
        ModelProviderDbContext db,
        ICredentialProtector protector,
        IModelProviderAdapterFactory adapterFactory)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(adapterFactory);
        _db = db;
        _protector = protector;
        _adapterFactory = adapterFactory;
    }

    /// <inheritdoc />
    public async Task<Result<ChatResponse>> ChatAsync(long modelId, ChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resolved = await ResolveAsync(modelId, cancellationToken);
        if (resolved.Code != 200)
        {
            return Result<ChatResponse>.Fail(resolved.Code, resolved.Message);
        }

        var target = resolved.Data!;
        return await target.Adapter.ChatAsync(target.Connection, target.ModelName, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<IAsyncEnumerable<ChatStreamChunk>>> ChatStreamAsync(long modelId, ChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resolved = await ResolveAsync(modelId, cancellationToken);
        if (resolved.Code != 200)
        {
            return Result<IAsyncEnumerable<ChatStreamChunk>>.Fail(resolved.Code, resolved.Message);
        }

        var target = resolved.Data!;
        return await target.Adapter.ChatStreamAsync(target.Connection, target.ModelName, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<EmbeddingResponse>> EmbedAsync(long modelId, EmbeddingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resolved = await ResolveAsync(modelId, cancellationToken);
        if (resolved.Code != 200)
        {
            return Result<EmbeddingResponse>.Fail(resolved.Code, resolved.Message);
        }

        var target = resolved.Data!;
        return await target.Adapter.EmbedAsync(target.Connection, target.ModelName, request, cancellationToken);
    }

    private async Task<Result<ResolvedTarget>> ResolveAsync(long modelId, CancellationToken cancellationToken)
    {
        var model = await _db.Models.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == modelId, cancellationToken);
        if (model is null)
        {
            return Result<ResolvedTarget>.Fail((int)ProviderErrorCode.ModelNotFound, "模型不存在。");
        }

        if (!model.Enabled)
        {
            return Result<ResolvedTarget>.Fail((int)ProviderErrorCode.ModelDisabled, "模型已停用。");
        }

        var provider = await _db.Providers.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == model.ProviderId, cancellationToken);
        if (provider is null)
        {
            return Result<ResolvedTarget>.Fail((int)ProviderErrorCode.ProviderNotFound, "供应商不存在。");
        }

        if (!provider.Enabled)
        {
            return Result<ResolvedTarget>.Fail((int)ProviderErrorCode.ProviderDisabled, "供应商已停用。");
        }

        string apiKey;
        try
        {
            apiKey = _protector.Unprotect(provider.ApiKeyCipher);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return Result<ResolvedTarget>.Fail((int)ProviderErrorCode.CredentialError, "供应商密钥解密失败。");
        }

        IModelProviderAdapter adapter;
        try
        {
            adapter = _adapterFactory.Get(provider.ProviderType);
        }
        catch (NotSupportedException ex)
        {
            return Result<ResolvedTarget>.Fail((int)ProviderErrorCode.ProviderCallFailed, ex.Message);
        }

        var connection = new ProviderConnection
        {
            ProviderType = provider.ProviderType,
            BaseUrl = provider.BaseUrl,
            AuthType = provider.AuthType,
            AuthHeaderName = provider.AuthHeaderName,
            ApiKey = apiKey,
            Settings = provider.Settings,
        };

        return Result<ResolvedTarget>.Ok(new ResolvedTarget(adapter, connection, model.Name));
    }

    private sealed record ResolvedTarget(IModelProviderAdapter Adapter, ProviderConnection Connection, string ModelName);
}
