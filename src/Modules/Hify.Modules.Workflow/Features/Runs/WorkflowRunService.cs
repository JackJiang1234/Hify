using System.Text.Json;

using Hify.Modules.Workflow.Domain;
using Hify.Modules.Workflow.Features.Definitions;
using Hify.Modules.Workflow.Features.Execution;
using Hify.Modules.Workflow.Persistence;
using Hify.Shared.Pagination;
using Hify.Shared.Results;
using Hify.Shared.Time;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Workflow.Features.Runs;

/// <summary>
/// 工作流试运行 + 运行记录查询。一期同步执行：调引擎跑完，单次落 workflow_run（含最终态 + trace）。
/// 预检失败（工作流不存在 6001 / 定义非法 6002）返回失败 Result 不建 run；执行失败（节点错/超步/超时）
/// 仍落 run 并以 Ok 返回（run.status=failed + trace），便于前端展示。
/// </summary>
internal sealed class WorkflowRunService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly WorkflowDbContext _db;
    private readonly WorkflowEngine _engine;
    private readonly DefinitionValidator _validator;
    private readonly IClock _clock;

    public WorkflowRunService(WorkflowDbContext db, WorkflowEngine engine, DefinitionValidator validator, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _engine = engine;
        _validator = validator;
        _clock = clock;
    }

    public async Task<Result<WorkflowRunDto>> RunAsync(long workflowId, CreateRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workflow = await _db.Workflows.AsNoTracking().FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);
        if (workflow is null)
        {
            return Result<WorkflowRunDto>.Fail((int)WorkflowErrorCode.WorkflowNotFound, "工作流不存在。");
        }

        var validation = _validator.Validate(workflow.Definition);
        if (validation.Code != 200 || validation.Data is null)
        {
            return Result<WorkflowRunDto>.Fail(validation.Code, validation.Message);
        }

        var inputs = request.Inputs.ToDictionary(
            pair => pair.Key,
            pair => (object?)pair.Value,
            StringComparer.Ordinal);

        var startedAt = _clock.UtcNowEpochMs;
        var execution = await _engine.ExecuteAsync(validation.Data, inputs, cancellationToken).ConfigureAwait(false);
        var finishedAt = _clock.UtcNowEpochMs;

        var run = new WorkflowRun
        {
            WorkflowId = workflowId,
            Status = execution.Status,
            Inputs = JsonSerializer.Serialize(request.Inputs, JsonOptions),
            Output = execution.Output,
            Trace = JsonSerializer.Serialize(execution.Trace, JsonOptions),
            ErrorMessage = execution.ErrorMessage,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
        };
        _db.WorkflowRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<WorkflowRunDto>.Ok(WorkflowRunMapping.ToDto(run));
    }

    public async Task<PageResult<WorkflowRunDto>> ListAsync(long workflowId, int page, int size, CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Of(page, size);
        var query = _db.WorkflowRuns.AsNoTracking().Where(run => run.WorkflowId == workflowId);

        var items = await query.ApplyPage(pageRequest)
            .Select(run => WorkflowRunMapping.ToDto(run))
            .ToListAsync(cancellationToken);
        var total = pageRequest.IsFirstPage ? await query.CountAsync(cancellationToken) : 0;

        return PageResult<WorkflowRunDto>.Ok(items, total, pageRequest.Page, pageRequest.Size);
    }

    public async Task<Result<WorkflowRunDto>> GetAsync(long workflowId, long runId, CancellationToken cancellationToken)
    {
        var run = await _db.WorkflowRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId && r.WorkflowId == workflowId, cancellationToken);
        return run is null
            ? Result<WorkflowRunDto>.Fail((int)WorkflowErrorCode.RunNotFound, "运行记录不存在。")
            : Result<WorkflowRunDto>.Ok(WorkflowRunMapping.ToDto(run));
    }
}
