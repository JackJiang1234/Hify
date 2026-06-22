using System.Text.Json;

using Hify.Modules.Conversation.Features.Chat;

namespace Hify.Modules.Conversation.Tests.Features.Chat;

/// <summary>SSE 帧负载序列化（camelCase JSON，对齐设计 §6 的事件协议）。</summary>
public sealed class ChatEventSerializerTests
{
    [Fact]
    public void Serialize_Delta_EmitsTypeAndText()
    {
        var json = ChatEventSerializer.Serialize(ChatEvent.Delta("hi"));

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("delta", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("hi", doc.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void Serialize_Done_EmitsCamelCaseFields()
    {
        var json = ChatEventSerializer.Serialize(ChatEvent.Done(42, "stop", 11, 7));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("done", root.GetProperty("type").GetString());
        Assert.Equal(42, root.GetProperty("messageId").GetInt64());
        Assert.Equal("stop", root.GetProperty("finishReason").GetString());
        Assert.Equal(11, root.GetProperty("promptTokens").GetInt64());
        Assert.Equal(7, root.GetProperty("completionTokens").GetInt64());
    }

    [Fact]
    public void Serialize_Error_EmitsCodeAndMessage()
    {
        var json = ChatEventSerializer.Serialize(ChatEvent.Error(4005, "上游出错"));

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(4005, doc.RootElement.GetProperty("code").GetInt32());
        Assert.Equal("上游出错", doc.RootElement.GetProperty("message").GetString());
    }
}
