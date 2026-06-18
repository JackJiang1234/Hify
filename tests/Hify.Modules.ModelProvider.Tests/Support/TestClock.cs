using Hify.Shared.Time;

namespace Hify.Modules.ModelProvider.Tests.Support;

/// <summary>固定时间源，便于断言 checked_at 等时间字段。</summary>
internal sealed class TestClock : IClock
{
    public long UtcNowEpochMs { get; set; } = 1_700_000_000_000;
}
