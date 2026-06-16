using System.Text;

using Hify.Shared.Streaming;

namespace Hify.Shared.Tests;

public class SseEventWriterTests
{
    private static async Task<string> Capture(Func<SseEventWriter, Task> write)
    {
        using var stream = new MemoryStream();
        var writer = new SseEventWriter(stream);
        await write(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public async Task WriteEvent_FormatsDataWithTrailingBlankLine()
    {
        var output = await Capture(writer => writer.WriteEventAsync("hello", CancellationToken.None));

        Assert.Equal("data: hello\n\n", output);
    }

    [Fact]
    public async Task WriteEvent_WithEventType_EmitsEventField()
    {
        var output = await Capture(writer => writer.WriteEventAsync("hi", CancellationToken.None, eventType: "message"));

        Assert.Equal("event: message\ndata: hi\n\n", output);
    }

    [Fact]
    public async Task WriteEvent_MultilineData_PrefixesEachLine()
    {
        var output = await Capture(writer => writer.WriteEventAsync("a\nb", CancellationToken.None));

        Assert.Equal("data: a\ndata: b\n\n", output);
    }

    [Fact]
    public async Task WriteEvent_NormalizesCrlf()
    {
        var output = await Capture(writer => writer.WriteEventAsync("a\r\nb", CancellationToken.None));

        Assert.Equal("data: a\ndata: b\n\n", output);
    }

    [Fact]
    public async Task WriteComment_EmitsColonPrefixedHeartbeat()
    {
        var output = await Capture(writer => writer.WriteCommentAsync("ping", CancellationToken.None));

        Assert.Equal(": ping\n\n", output);
    }
}
