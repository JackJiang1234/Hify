using Hify.Modules.Workflow.Features.Definitions;

namespace Hify.Modules.Workflow.Tests.Features.Definitions;

/// <summary>
/// 定义校验器的表驱动单测（纯函数，不连库）。覆盖设计 §6 六条图约束 + 解析失败。
/// </summary>
public sealed class DefinitionValidatorTests
{
    private const int InvalidDefinitionCode = 6002;

    private static readonly DefinitionValidator Validator = new();

    private static string Node(string id, string type, string? config = null) =>
        config is null
            ? $$"""{ "id": "{{id}}", "type": "{{type}}" }"""
            : $$"""{ "id": "{{id}}", "type": "{{type}}", "config": {{config}} }""";

    private static string Edge(string id, string source, string target, string handle = "") =>
        $$"""{ "id": "{{id}}", "source": "{{source}}", "target": "{{target}}", "sourceHandle": "{{handle}}" }""";

    private static string Def(IEnumerable<string> nodes, IEnumerable<string> edges) =>
        $$"""{ "version": "1", "nodes": [ {{string.Join(",", nodes)}} ], "edges": [ {{string.Join(",", edges)}} ] }""";

    [Fact]
    public void Validate_LinearStartLlmEnd_Succeeds()
    {
        var json = Def(
            [
                Node("s", "start"),
                Node("l", "llm", """{ "prompt": "hi {{s.user_input}}" }"""),
                Node("e", "end", """{ "output": "{{l.text}}" }"""),
            ],
            [Edge("e1", "s", "l"), Edge("e2", "l", "e")]);

        var result = Validator.Validate(json);

        Assert.Equal(200, result.Code);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data!.Nodes.Count);
    }

    [Fact]
    public void Validate_ConditionSingleLevelBranch_Succeeds()
    {
        var json = Def(
            [
                Node("s", "start"),
                Node("c", "condition", """{ "cases": [ { "handle": "c1", "left": "{{s.text}}", "op": "contains", "right": "x" } ] }"""),
                Node("a", "llm"),
                Node("e1", "end"),
                Node("e2", "end"),
            ],
            [
                Edge("e1", "s", "c"),
                Edge("e2", "c", "a", "c1"),
                Edge("e3", "c", "e1", "else"),
                Edge("e4", "a", "e2"),
            ]);

        var result = Validator.Validate(json);

        Assert.Equal(200, result.Code);
    }

    public static TheoryData<string, string> InvalidCases()
    {
        var data = new TheoryData<string, string>();

        // 解析失败。
        data.Add("not-json", "格式错误的 JSON");
        data.Add("", "空字符串");

        // 缺 start。
        data.Add(Def([Node("e", "end")], []), "无 start 节点");

        // 两个 start。
        data.Add(
            Def([Node("s1", "start"), Node("s2", "start"), Node("e", "end")],
                [Edge("x", "s1", "e")]),
            "两个 start 节点");

        // 缺 end。
        data.Add(
            Def([Node("s", "start"), Node("l", "llm")], [Edge("x", "s", "l")]),
            "无 end 节点");

        // 节点 Id 重复。
        data.Add(
            Def([Node("s", "start"), Node("s", "end")], []),
            "节点 Id 重复");

        // 节点类型非法。
        data.Add(
            Def([Node("s", "start"), Node("k", "code"), Node("e", "end")],
                [Edge("e1", "s", "k"), Edge("e2", "k", "e")]),
            "类型非法 code");

        // 连线引用不存在的节点。
        data.Add(
            Def([Node("s", "start"), Node("e", "end")], [Edge("x", "s", "ghost")]),
            "连线目标不存在");

        // 非 condition 多出边。
        data.Add(
            Def([Node("s", "start"), Node("a", "llm"), Node("e1", "end"), Node("e2", "end")],
                [Edge("x", "s", "a"), Edge("o1", "a", "e1"), Edge("o2", "a", "e2")]),
            "llm 多出边");

        // 非 start 多入边（汇合）。
        data.Add(
            Def([Node("s", "start"), Node("a", "llm"), Node("b", "llm"), Node("t", "end")],
                [Edge("x", "s", "a"), Edge("i1", "a", "t"), Edge("i2", "b", "t")]),
            "end 多入边汇合");

        // start 有入边。
        data.Add(
            Def([Node("s", "start"), Node("a", "llm"), Node("e", "end")],
                [Edge("bad", "a", "s"), Edge("x", "s", "e")]),
            "start 有入边");

        // end 有出边。
        data.Add(
            Def([Node("s", "start"), Node("e", "end"), Node("x", "llm")],
                [Edge("a", "s", "e"), Edge("bad", "e", "x")]),
            "end 有出边");

        // 存在环（脱离 start 的环，避免先撞多入边）。
        data.Add(
            Def([Node("s", "start"), Node("e", "end"), Node("a", "llm"), Node("b", "llm")],
                [Edge("x", "s", "e"), Edge("c1", "a", "b"), Edge("c2", "b", "a")]),
            "环 a<->b");

        // condition 出边无 handle。
        data.Add(
            Def([Node("s", "start"), Node("c", "condition"), Node("e", "end")],
                [Edge("x", "s", "c"), Edge("bad", "c", "e")]),
            "condition 出边缺 handle");

        // 变量引用不存在的节点。
        data.Add(
            Def([Node("s", "start"), Node("e", "end", """{ "output": "{{ghost.text}}" }""")],
                [Edge("x", "s", "e")]),
            "引用不存在节点 ghost");

        // 变量引用非前驱（引用了不在前驱链上的节点 b）。
        data.Add(
            Def([Node("s", "start"), Node("a", "llm", """{ "prompt": "{{b.text}}" }"""), Node("b", "llm"), Node("e", "end")],
                [Edge("x", "s", "a"), Edge("y", "a", "e")]),
            "引用非前驱 b");

        return data;
    }

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void Validate_InvalidDefinition_FailsWith6002(string json, string because)
    {
        var result = Validator.Validate(json);

        Assert.Equal(InvalidDefinitionCode, result.Code);
        Assert.Null(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Message), because);
    }
}
