namespace Hify.Shared.Persistence;

/// <summary>
/// 软删除标记。实现该接口的实体会被 <see cref="HifyDbContext"/> 自动应用全局查询过滤
/// （<c>deleted_at = 0</c>）并将物理删除转为软删除。
/// </summary>
public interface ISoftDelete
{
    /// <summary>删除时刻（epoch ms）。0 表示未删除，非 0 表示删除时刻。</summary>
    long DeletedAt { get; set; }
}
