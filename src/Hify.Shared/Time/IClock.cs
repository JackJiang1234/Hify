namespace Hify.Shared.Time;

/// <summary>
/// 时间源抽象。统一以 epoch 毫秒（UTC）表达时间，对齐数据库「时间字段用 bigint 存 epoch ms」规范，
/// 并使依赖时间的逻辑（审计字段、软删时刻）可在测试中替换为固定时钟。
/// </summary>
public interface IClock
{
    /// <summary>当前 UTC 时间的 Unix 毫秒时间戳。</summary>
    long UtcNowEpochMs { get; }
}
