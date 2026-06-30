using System.Text.Json;

using Hify.Modules.Workflow.Features.Execution;

namespace Hify.Modules.Workflow.Tests.Features.Execution;

/// <summary>变量解析器的表驱动单测（纯函数，不连库）。覆盖插值、嵌套取值、缺失兜底、类型字符串化。</summary>
public sealed class VariableResolverTests
{
    private static readonly VariableResolver Resolver = new();

    // 构造 nodeId -> (field -> value) 的输出表。
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Outputs(
        params (string NodeId, IReadOnlyDictionary<string, object?> Fields)[] entries)
    {
        var map = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var (nodeId, fields) in entries)
        {
            map[nodeId] = fields;
        }

        return map;
    }

    private static IReadOnlyDictionary<string, object?> Fields(params (string Key, object? Value)[] pairs)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            dict[key] = value;
        }

        return dict;
    }

    [Fact]
    public void ResolveString_NoReferences_ReturnsUnchanged()
    {
        var outputs = Outputs(("s", Fields(("x", "1"))));
        Assert.Equal("plain text", Resolver.ResolveString("plain text", outputs));
    }

    [Fact]
    public void ResolveString_SingleReference_Substitutes()
    {
        var outputs = Outputs(("s", Fields(("name", "Alice"))));
        Assert.Equal("hi Alice", Resolver.ResolveString("hi {{s.name}}", outputs));
    }

    [Fact]
    public void ResolveString_MultipleReferences_AllSubstituted()
    {
        var outputs = Outputs(
            ("s", Fields(("a", "X"))),
            ("l", Fields(("text", "Y"))));
        Assert.Equal("X-Y-X", Resolver.ResolveString("{{s.a}}-{{l.text}}-{{s.a}}", outputs));
    }

    [Fact]
    public void ResolveString_NestedDictionaryPath_Descends()
    {
        var outputs = Outputs(("s", Fields(("user", Fields(("name", "Bob"))))));
        Assert.Equal("Bob", Resolver.ResolveString("{{s.user.name}}", outputs));
    }

    [Fact]
    public void ResolveString_JsonElementObjectPath_Descends()
    {
        using var doc = JsonDocument.Parse("""{ "x": 42 }""");
        var outputs = Outputs(("s", Fields(("data", doc.RootElement.Clone()))));
        Assert.Equal("42", Resolver.ResolveString("{{s.data.x}}", outputs));
    }

    [Fact]
    public void ResolveString_MissingNode_ResolvesToEmpty()
    {
        var outputs = Outputs(("s", Fields(("x", "1"))));
        Assert.Equal("[]", Resolver.ResolveString("[{{ghost.x}}]", outputs));
    }

    [Fact]
    public void ResolveString_MissingField_ResolvesToEmpty()
    {
        var outputs = Outputs(("s", Fields(("x", "1"))));
        Assert.Equal("[]", Resolver.ResolveString("[{{s.missing}}]", outputs));
    }

    [Fact]
    public void ResolveString_NumberAndBool_Stringified()
    {
        var outputs = Outputs(("s", Fields(("n", 42), ("ok", true))));
        Assert.Equal("42/true", Resolver.ResolveString("{{s.n}}/{{s.ok}}", outputs));
    }

    [Fact]
    public void ResolveString_DoubleUsesInvariantCulture()
    {
        var outputs = Outputs(("s", Fields(("d", 3.5d))));
        Assert.Equal("3.5", Resolver.ResolveString("{{s.d}}", outputs));
    }

    [Fact]
    public void TryResolveValue_Found_ReturnsRawValueForTypedCompare()
    {
        var outputs = Outputs(("s", Fields(("n", 42))));
        var ok = Resolver.TryResolveValue(new VariableRef.Reference("s", "n"), outputs, out var value);

        Assert.True(ok);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryResolveValue_Missing_ReturnsFalse()
    {
        var outputs = Outputs(("s", Fields(("n", 42))));
        Assert.False(Resolver.TryResolveValue(new VariableRef.Reference("s", "absent"), outputs, out _));
        Assert.False(Resolver.TryResolveValue(new VariableRef.Reference("ghost", "n"), outputs, out _));
    }
}
