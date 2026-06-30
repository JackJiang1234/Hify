using System.Text.Json;

using Hify.Modules.Workflow.Domain;
using Hify.Modules.Workflow.Persistence;
using Hify.Shared.Pagination;
using Hify.Shared.Results;

using Microsoft.EntityFrameworkCore;

namespace Hify.Modules.Workflow.Features.Definitions;

/// <summary>
/// 工作流定义 CRUD + 发布应用服务。保存仅校验 JSON 合法（草稿态），发布时跑完整图校验（<see cref="DefinitionValidator"/>）。
/// 任何定义改动都把状态退回 draft，须重新发布。可预期失败返回 <see cref="Result{T}"/>（6xxx），不抛异常。
/// </summary>
internal sealed class WorkflowService
{
    private readonly WorkflowDbContext _db;
    private readonly DefinitionValidator _validator;

    public WorkflowService(WorkflowDbContext db, DefinitionValidator validator)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(validator);
        _db = db;
        _validator = validator;
    }

    public async Task<Result<WorkflowDto>> CreateAsync(CreateWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsWellFormedJson(request.Definition))
        {
            return Fail("定义不是合法 JSON。");
        }

        if (await NameExistsAsync(request.Name, 0, cancellationToken))
        {
            return Result<WorkflowDto>.Fail((int)WorkflowErrorCode.NameConflict, $"工作流名称已存在：{request.Name}。");
        }

        var workflow = new Domain.Workflow
        {
            Name = request.Name,
            Description = request.Description,
            Definition = string.IsNullOrWhiteSpace(request.Definition) ? "{}" : request.Definition,
            Status = WorkflowStatus.Draft,
        };
        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<WorkflowDto>.Ok(WorkflowMapping.ToDto(workflow));
    }

    public async Task<PageResult<WorkflowDto>> ListAsync(int page, int size, CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Of(page, size);
        var query = _db.Workflows.AsNoTracking();

        var items = await query.ApplyPage(pageRequest)
            .Select(workflow => WorkflowMapping.ToDto(workflow))
            .ToListAsync(cancellationToken);
        var total = pageRequest.IsFirstPage ? await query.CountAsync(cancellationToken) : 0;

        return PageResult<WorkflowDto>.Ok(items, total, pageRequest.Page, pageRequest.Size);
    }

    public async Task<Result<WorkflowDto>> GetAsync(long id, CancellationToken cancellationToken)
    {
        var workflow = await _db.Workflows.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        return workflow is null
            ? NotFound()
            : Result<WorkflowDto>.Ok(WorkflowMapping.ToDto(workflow));
    }

    public async Task<Result<WorkflowDto>> UpdateAsync(long id, UpdateWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workflow = await _db.Workflows.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (workflow is null)
        {
            return NotFound();
        }

        if (!IsWellFormedJson(request.Definition))
        {
            return Fail("定义不是合法 JSON。");
        }

        if (await NameExistsAsync(request.Name, id, cancellationToken))
        {
            return Result<WorkflowDto>.Fail((int)WorkflowErrorCode.NameConflict, $"工作流名称已存在：{request.Name}。");
        }

        workflow.Name = request.Name;
        workflow.Description = request.Description;
        workflow.Definition = string.IsNullOrWhiteSpace(request.Definition) ? "{}" : request.Definition;
        // 定义可能已变，退回草稿，须重新发布校验。
        workflow.Status = WorkflowStatus.Draft;
        await _db.SaveChangesAsync(cancellationToken);

        return Result<WorkflowDto>.Ok(WorkflowMapping.ToDto(workflow));
    }

    public async Task<Result<WorkflowDto>> PublishAsync(long id, CancellationToken cancellationToken)
    {
        var workflow = await _db.Workflows.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (workflow is null)
        {
            return NotFound();
        }

        var validation = _validator.Validate(workflow.Definition);
        if (validation.Code != 200)
        {
            return Result<WorkflowDto>.Fail(validation.Code, validation.Message);
        }

        workflow.Status = WorkflowStatus.Published;
        await _db.SaveChangesAsync(cancellationToken);

        return Result<WorkflowDto>.Ok(WorkflowMapping.ToDto(workflow));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var workflow = await _db.Workflows.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (workflow is null)
        {
            return Result<bool>.Fail((int)WorkflowErrorCode.WorkflowNotFound, "工作流不存在。");
        }

        _db.Workflows.Remove(workflow);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }

    private Task<bool> NameExistsAsync(string name, long excludeId, CancellationToken cancellationToken) =>
        _db.Workflows.AnyAsync(w => w.Name == name && w.Id != excludeId, cancellationToken);

    private static bool IsWellFormedJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true; // 空定义存为 "{}"。
        }

        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Result<WorkflowDto> NotFound() =>
        Result<WorkflowDto>.Fail((int)WorkflowErrorCode.WorkflowNotFound, "工作流不存在。");

    private static Result<WorkflowDto> Fail(string message) =>
        Result<WorkflowDto>.Fail((int)WorkflowErrorCode.InvalidDefinition, message);
}
