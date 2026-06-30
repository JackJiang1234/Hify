using Hify.Modules.Workflow.Features.Definitions;
using Hify.Modules.Workflow.Features.Execution;
using Hify.Modules.Workflow.Features.Execution.Nodes;
using Hify.Modules.Workflow.Features.Runs;
using Hify.Modules.Workflow.Persistence;
using Hify.Modules.Workflow.Tests.Support;

namespace Hify.Modules.Workflow.Tests.Features;

/// <summary>
/// 服务层在真实 PostgreSQL 上的集成测试（CRUD + 发布校验 + 试运行落库）。
/// 连不上则静默跳过；每个用例在事务内执行不提交，结束回滚，保证零残留。
/// 前置：docker compose up -d（首次自动应用 ddl.sql）。
/// </summary>
public sealed class WorkflowServiceIntegrationTests : IAsyncLifetime
{
    // 合法流水线：start → llm → end（含变量引用）。
    private const string ValidDefinition = """
        {
          "version": "1",
          "nodes": [
            { "id": "s", "type": "start", "config": { "inputs": [ { "name": "q", "required": true } ] } },
            { "id": "l", "type": "llm", "config": { "modelId": 1, "prompt": "{{s.q}}" } },
            { "id": "e", "type": "end", "config": { "output": "{{l.text}}" } }
          ],
          "edges": [
            { "id": "e1", "source": "s", "target": "l" },
            { "id": "e2", "source": "l", "target": "e" }
          ]
        }
        """;

    private bool _available;

    public async Task InitializeAsync() => _available = await WorkflowTestDb.IsAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static WorkflowService NewWorkflowService(WorkflowDbContext context) =>
        new(context, new DefinitionValidator());

    private static WorkflowRunService NewRunService(WorkflowDbContext context)
    {
        var resolver = new VariableResolver();
        INodeHandler[] handlers =
        [
            new StartNodeHandler(),
            new LlmNodeHandler(FakeModelInvoker.Returning("answer-42"), resolver),
            new ToolNodeHandler(FakeMcpToolInvoker.Returning("tool-out"), resolver),
            new ConditionNodeHandler(resolver),
            new EndNodeHandler(resolver),
        ];
        return new WorkflowRunService(context, new WorkflowEngine(handlers), new DefinitionValidator(), new WorkflowTestDb.FixedClock());
    }

    private static CreateWorkflowRequest CreateRequest(string name, string definition) =>
        new() { Name = name, Description = "it", Definition = definition };

    [Fact]
    public async Task Create_ThenGet_RoundTrips()
    {
        if (!_available)
        {
            return;
        }

        await using var context = WorkflowTestDb.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var service = NewWorkflowService(context);

        var created = await service.CreateAsync(CreateRequest("wf-create", ValidDefinition), CancellationToken.None);

        Assert.Equal(200, created.Code);
        Assert.Equal("draft", created.Data!.Status);

        var fetched = await service.GetAsync(created.Data.Id, CancellationToken.None);
        Assert.Equal(200, fetched.Code);
        Assert.Equal("wf-create", fetched.Data!.Name);
    }

    [Fact]
    public async Task Create_DuplicateName_FailsWith6008()
    {
        if (!_available)
        {
            return;
        }

        await using var context = WorkflowTestDb.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var service = NewWorkflowService(context);

        await service.CreateAsync(CreateRequest("wf-dup", "{}"), CancellationToken.None);
        var second = await service.CreateAsync(CreateRequest("wf-dup", "{}"), CancellationToken.None);

        Assert.Equal(6008, second.Code);
    }

    [Fact]
    public async Task Publish_InvalidGraph_FailsWith6002_StaysDraft()
    {
        if (!_available)
        {
            return;
        }

        await using var context = WorkflowTestDb.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var service = NewWorkflowService(context);

        // 合法 JSON 但非法图（无 start/end）。
        var created = await service.CreateAsync(CreateRequest("wf-bad", """{ "nodes": [], "edges": [] }"""), CancellationToken.None);
        var published = await service.PublishAsync(created.Data!.Id, CancellationToken.None);

        Assert.Equal(6002, published.Code);

        var fetched = await service.GetAsync(created.Data.Id, CancellationToken.None);
        Assert.Equal("draft", fetched.Data!.Status);
    }

    [Fact]
    public async Task Publish_ValidGraph_Succeeds()
    {
        if (!_available)
        {
            return;
        }

        await using var context = WorkflowTestDb.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var service = NewWorkflowService(context);

        var created = await service.CreateAsync(CreateRequest("wf-ok", ValidDefinition), CancellationToken.None);
        var published = await service.PublishAsync(created.Data!.Id, CancellationToken.None);

        Assert.Equal(200, published.Code);
        Assert.Equal("published", published.Data!.Status);
    }

    [Fact]
    public async Task Run_PersistsSucceededRunWithOutputAndTrace()
    {
        if (!_available)
        {
            return;
        }

        await using var context = WorkflowTestDb.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var workflows = NewWorkflowService(context);
        var runs = NewRunService(context);

        var created = await workflows.CreateAsync(CreateRequest("wf-run", ValidDefinition), CancellationToken.None);
        var request = new CreateRunRequest { Inputs = new Dictionary<string, string> { ["q"] = "hello" } };

        var run = await runs.RunAsync(created.Data!.Id, request, CancellationToken.None);

        Assert.Equal(200, run.Code);
        Assert.Equal("succeeded", run.Data!.Status);
        Assert.Equal("answer-42", run.Data.Output);
        Assert.Contains("\"nodeId\"", run.Data.Trace);
        Assert.NotEqual(0, run.Data.StartedAt);

        var listed = await runs.ListAsync(created.Data.Id, 1, 20, CancellationToken.None);
        Assert.Equal(200, listed.Code);
        Assert.Single(listed.Data!);
    }

    [Fact]
    public async Task Run_RequiredInputMissing_PersistsFailedRunWith6003InTrace()
    {
        if (!_available)
        {
            return;
        }

        await using var context = WorkflowTestDb.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var workflows = NewWorkflowService(context);
        var runs = NewRunService(context);

        var created = await workflows.CreateAsync(CreateRequest("wf-run-fail", ValidDefinition), CancellationToken.None);
        var run = await runs.RunAsync(created.Data!.Id, new CreateRunRequest(), CancellationToken.None);

        // 执行失败仍以 Ok 返回 run（status=failed），便于前端展示。
        Assert.Equal(200, run.Code);
        Assert.Equal("failed", run.Data!.Status);
        Assert.False(string.IsNullOrEmpty(run.Data.ErrorMessage));
    }

    [Fact]
    public async Task Delete_SoftDeletes_ThenGetReturns6001()
    {
        if (!_available)
        {
            return;
        }

        await using var context = WorkflowTestDb.NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var service = NewWorkflowService(context);

        var created = await service.CreateAsync(CreateRequest("wf-del", "{}"), CancellationToken.None);
        var deleted = await service.DeleteAsync(created.Data!.Id, CancellationToken.None);
        Assert.Equal(200, deleted.Code);

        var fetched = await service.GetAsync(created.Data.Id, CancellationToken.None);
        Assert.Equal(6001, fetched.Code);
    }
}
