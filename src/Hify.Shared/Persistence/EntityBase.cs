namespace Hify.Shared.Persistence;

/// <summary>
/// 实体基类。统一主键与审计/软删字段，对齐数据库规范：
/// bigint 主键（<see cref="Id"/>）、时间字段 bigint epoch ms、软删 <see cref="DeletedAt"/>（0=未删）。
/// 时间字段由 <see cref="HifyDbContext"/> 在保存时自动维护，业务代码无需手动赋值。
/// </summary>
public abstract class EntityBase : ISoftDelete
{
    /// <summary>主键。数据库为 <c>bigint GENERATED ALWAYS AS IDENTITY</c>。</summary>
    public long Id { get; set; }

    /// <summary>创建时刻（epoch ms）。新增时自动填充。</summary>
    public long CreatedAt { get; set; }

    /// <summary>最后更新时刻（epoch ms）。新增/修改时自动填充。</summary>
    public long UpdatedAt { get; set; }

    /// <inheritdoc />
    public long DeletedAt { get; set; }
}
