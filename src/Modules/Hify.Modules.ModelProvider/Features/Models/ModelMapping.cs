using Hify.Contracts.ModelProvider;

using DomainModel = Hify.Modules.ModelProvider.Domain.Model;

namespace Hify.Modules.ModelProvider.Features.Models;

/// <summary>模型实体 → DTO 映射。</summary>
internal static class ModelMapping
{
    public static ModelDto ToDto(DomainModel model) => new()
    {
        Id = model.Id,
        ProviderId = model.ProviderId,
        Name = model.Name,
        DisplayName = model.DisplayName,
        ModelType = model.ModelType,
        ContextWindow = model.ContextWindow,
        MaxOutputTokens = model.MaxOutputTokens,
        EmbeddingDimensions = model.EmbeddingDimensions,
        SupportsStreaming = model.SupportsStreaming,
        SupportsTools = model.SupportsTools,
        SupportsVision = model.SupportsVision,
        Source = model.Source,
        Enabled = model.Enabled,
        IsDefault = model.IsDefault,
        SortOrder = model.SortOrder,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt,
    };
}
