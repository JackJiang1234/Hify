using System.Text;

namespace Hify.Shared.Streaming;

/// <summary>
/// Server-Sent Events 写入器（基于 <see cref="Stream"/>，与具体 Web 框架无关）。
/// 负责事件帧格式化与逐事件刷新（禁缓冲）。控制器使用前须设置响应头：
/// <c>Content-Type: text/event-stream</c>、<c>Cache-Control: no-cache</c>，
/// 并对 Nginx 反代置 <c>X-Accel-Buffering: no</c> 以关闭缓冲（见规范）。
/// </summary>
public sealed class SseEventWriter
{
    /// <summary>SSE 响应内容类型。</summary>
    public const string ContentType = "text/event-stream";

    private readonly Stream _stream;

    /// <summary>基于给定输出流构造（通常为 <c>HttpResponse.Body</c>）。</summary>
    /// <param name="stream">输出流。</param>
    public SseEventWriter(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
    }

    /// <summary>写入一个事件并立即刷新。多行 <paramref name="data"/> 会按 SSE 规范逐行加 <c>data:</c>。</summary>
    /// <param name="data">事件数据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="eventType">可选事件名（<c>event:</c> 字段）。</param>
    public async Task WriteEventAsync(string data, CancellationToken cancellationToken, string? eventType = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        var builder = new StringBuilder();
        if (!string.IsNullOrEmpty(eventType))
        {
            builder.Append("event: ").Append(eventType).Append('\n');
        }

        foreach (var line in data.Split('\n'))
        {
            builder.Append("data: ").Append(line.TrimEnd('\r')).Append('\n');
        }

        // 空行表示事件结束。
        builder.Append('\n');

        await WriteRawAsync(builder.ToString(), cancellationToken);
    }

    /// <summary>写入注释行（<c>: ...</c>），常用于心跳保活。</summary>
    /// <param name="comment">注释内容。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task WriteCommentAsync(string comment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comment);
        await WriteRawAsync($": {comment}\n\n", cancellationToken);
    }

    private async Task WriteRawAsync(string text, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await _stream.WriteAsync(bytes, cancellationToken);
        // 立即推送，不积压在缓冲区。
        await _stream.FlushAsync(cancellationToken);
    }
}
