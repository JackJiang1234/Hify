namespace Hify.Shared.Time;

/// <summary>
/// 基于系统时钟的 <see cref="IClock"/> 实现。注册为单例。
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public long UtcNowEpochMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
