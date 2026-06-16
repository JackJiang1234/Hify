using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hify.Shared.Persistence;

/// <summary>
/// 集中实现数据库强制约定，供 <see cref="HifyDbContext"/> 在 <c>OnModelCreating</c> 调用，
/// 避免每个模块各自手写：snake_case 命名、枚举存 varchar(32)、软删全局过滤。
/// </summary>
public static class ModelBuilderConventions
{
    /// <summary>枚举字段统一存储长度，对齐规范「枚举用 varchar(32)」。</summary>
    public const int EnumColumnLength = 32;

    /// <summary>应用全部 Hify 数据库约定。</summary>
    /// <param name="modelBuilder">模型构造器。</param>
    public static void ApplyHifyConventions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ApplyEnumAsVarchar(modelBuilder);
        ApplySoftDeleteQueryFilter(modelBuilder);
        ApplySnakeCaseNames(modelBuilder);
    }

    // 枚举 → varchar(32)：禁用原生 ENUM，存可读字符串。
    private static void ApplyEnumAsVarchar(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (!clrType.IsEnum)
                {
                    continue;
                }

                var converterType = typeof(EnumToStringConverter<>).MakeGenericType(clrType);
                var converter = (ValueConverter)Activator.CreateInstance(converterType)!;
                property.SetValueConverter(converter);
                property.SetMaxLength(EnumColumnLength);
            }
        }
    }

    // 软删全局过滤：对所有 ISoftDelete 实体加 deleted_at = 0 过滤，查询默认不含已删数据。
    private static void ApplySoftDeleteQueryFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var deletedAt = Expression.Property(parameter, nameof(ISoftDelete.DeletedAt));
            var notDeleted = Expression.Equal(deletedAt, Expression.Constant(0L));
            var filter = Expression.Lambda(notDeleted, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    // 表名/列名 → snake_case：与数据库规范一致，C# 侧仍用 PascalCase。
    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is not null)
            {
                entityType.SetTableName(SnakeCaseNaming.ToSnakeCase(tableName));
            }

            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(SnakeCaseNaming.ToSnakeCase(property.Name));
            }
        }
    }
}
