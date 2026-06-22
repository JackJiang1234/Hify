using Hify.Contracts.Agent;
using Hify.Modules.Agent.Features.Agents;
using Hify.Modules.Agent.Tests.Support;

namespace Hify.Modules.Agent.Tests.Features.Agents;

/// <summary>
/// 跨模块只读查询 <see cref="Hify.Contracts.Agent.IAgentQuery"/> 的真实库测试（连不上则跳过）。
/// 供对话引擎（L2）运行时装配 Agent 配置；仅按 Id 取，存在性/启用判断交调用方。
/// </summary>
public sealed class AgentQueryTests : IAsyncLifetime
{
    private const long ChatModelId = 1;
    private bool _available;

    public async Task InitializeAsync() => _available = await AgentTestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static FakeModelProviderQuery FakeWithChatModel() =>
        new FakeModelProviderQuery().Add(FakeModelProviderQuery.ChatModel(ChatModelId));

    private static CreateAgentRequest NewAgent(string name, bool enabled = true) => new()
    {
        Name = name,
        ModelId = ChatModelId,
        SystemPrompt = "you are helpful",
        MaxIterations = 5,
        RetrievalParams = new RetrievalParams { TopK = 3 },
        ToolIds = [10, 11],
        KnowledgeBaseIds = [20],
        Enabled = enabled,
    };

    private static string UniqueName() => $"itq-{Guid.NewGuid():N}";

    [Fact]
    public async Task GetAgentAsync_Existing_ReturnsDtoWithBindings()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var created = await new AgentService(db, FakeWithChatModel()).CreateAsync(NewAgent(UniqueName()), CancellationToken.None);

        await using var verify = AgentTestDb.NewContext();
        var query = new AgentQuery(verify);
        var result = await query.GetAgentAsync(created.Data!.Id, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.Equal(created.Data.Id, result.Data!.Id);
        Assert.Equal(ChatModelId, result.Data.ModelId);
        Assert.Equal("you are helpful", result.Data.SystemPrompt);
        Assert.Equal(new long[] { 10, 11 }, result.Data.ToolIds.OrderBy(x => x));
        Assert.Equal(new long[] { 20 }, result.Data.KnowledgeBaseIds);
        Assert.True(result.Data.Enabled);
    }

    [Fact]
    public async Task GetAgentAsync_Missing_ReturnsNotFound()
    {
        if (!_available)
        {
            return;
        }

        await using var db = AgentTestDb.NewContext();
        var result = await new AgentQuery(db).GetAgentAsync(999_999_999, CancellationToken.None);

        Assert.Equal(3001, result.Code); // AgentNotFound
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetAgentAsync_Disabled_ReturnsDtoWithEnabledFalse()
    {
        if (!_available)
        {
            return;
        }

        // 停用 Agent 仍可查到（Enabled=false），是否拒绝由调用方（对话引擎 4002）决定。
        await using var db = AgentTestDb.NewContext();
        var created = await new AgentService(db, FakeWithChatModel())
            .CreateAsync(NewAgent(UniqueName(), enabled: false), CancellationToken.None);

        await using var verify = AgentTestDb.NewContext();
        var result = await new AgentQuery(verify).GetAgentAsync(created.Data!.Id, CancellationToken.None);

        Assert.Equal(200, result.Code);
        Assert.False(result.Data!.Enabled);
    }
}
