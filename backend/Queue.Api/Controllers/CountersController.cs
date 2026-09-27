using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Queue.Api.Common;
using Queue.Application.Dtos;
using Queue.Application.Services;
using Queue.Domain.Common;
using Queue.Domain.Enums;

namespace Queue.Api.Controllers;

/// <summary>
/// 櫃台叫號作業（§11.2 Counter API、§5.4 櫃台）
/// </summary>
[ApiController]
[Route("api/queue/counters")]
[ApiExplorerSettings(GroupName = SwaggerGroups.Counter)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
public class CountersController : ControllerBase
{
    private const string OperatorRoles = $"{QueueRoles.Admin},{QueueRoles.Manager},{QueueRoles.Counter}";
    private const string AdminRoles = $"{QueueRoles.Admin},{QueueRoles.Manager}";

    private readonly CounterService _counter;
    private readonly QueueCounterService _crud;

    public CountersController(CounterService counter, QueueCounterService crud)
    {
        _counter = counter;
        _crud = crud;
    }

    // ─────────────── 櫃台作業（叫號） ───────────────

    /// <summary>下一號（POST /api/queue/counters/{counterId}/call-next）</summary>
    [HttpPost("{counterId:long}/call-next")]
    [Authorize(Roles = OperatorRoles)]
    public async Task<ActionResult<ApiResponse<CallNextResultDto>>> CallNext(long counterId, CancellationToken ct)
        => Ok(ApiResponse<CallNextResultDto>.Ok(await _counter.CallNextAsync(counterId, ct), "叫號成功"));

    /// <summary>再叫一次（POST /api/queue/counters/{counterId}/recall）</summary>
    [HttpPost("{counterId:long}/recall")]
    [Authorize(Roles = OperatorRoles)]
    public async Task<ActionResult<ApiResponse<TicketStatusDto>>> Recall(long counterId, CancellationToken ct)
        => Ok(ApiResponse<TicketStatusDto>.Ok(await _counter.RecallAsync(counterId, ct), "已重新叫號"));

    /// <summary>開始服務（POST /api/queue/counters/{counterId}/start）</summary>
    [HttpPost("{counterId:long}/start")]
    [Authorize(Roles = OperatorRoles)]
    public async Task<ActionResult<ApiResponse<TicketStatusDto>>> Start(long counterId, CancellationToken ct)
        => Ok(ApiResponse<TicketStatusDto>.Ok(await _counter.StartAsync(counterId, ct), "已開始服務"));

    /// <summary>完成服務（POST /api/queue/counters/{counterId}/complete）</summary>
    [HttpPost("{counterId:long}/complete")]
    [Authorize(Roles = OperatorRoles)]
    public async Task<ActionResult<ApiResponse<TicketStatusDto>>> Complete(long counterId, [FromBody] CompleteRequest? request, CancellationToken ct)
        => Ok(ApiResponse<TicketStatusDto>.Ok(await _counter.CompleteAsync(counterId, request?.Remark, ct), "已完成服務"));

    /// <summary>過號（POST /api/queue/counters/{counterId}/no-show）</summary>
    [HttpPost("{counterId:long}/no-show")]
    [Authorize(Roles = OperatorRoles)]
    public async Task<ActionResult<ApiResponse<TicketStatusDto>>> NoShow(long counterId, [FromBody] CompleteRequest? request, CancellationToken ct)
        => Ok(ApiResponse<TicketStatusDto>.Ok(await _counter.NoShowAsync(counterId, request?.Remark, ct), "已標記過號"));

    /// <summary>轉移（POST /api/queue/counters/{counterId}/transfer）</summary>
    [HttpPost("{counterId:long}/transfer")]
    [Authorize(Roles = OperatorRoles)]
    public async Task<ActionResult<ApiResponse<TicketStatusDto>>> Transfer(long counterId, [FromBody] TransferRequest request, CancellationToken ct)
        => Ok(ApiResponse<TicketStatusDto>.Ok(await _counter.TransferAsync(counterId, request, ct), "已轉移"));

    /// <summary>暫停/恢復服務（POST /api/queue/counters/{counterId}/pause）</summary>
    [HttpPost("{counterId:long}/pause")]
    [Authorize(Roles = OperatorRoles)]
    public async Task<ActionResult<ApiResponse<CounterDto>>> Pause(long counterId, [FromBody] PauseRequest request, CancellationToken ct)
        => Ok(ApiResponse<CounterDto>.Ok(await _counter.PauseAsync(counterId, request.Paused, ct), request.Paused ? "已暫停" : "已恢復"));

    /// <summary>櫃台目前狀態</summary>
    [HttpGet("{counterId:long}/status")]
    [Authorize(Roles = OperatorRoles)]
    public async Task<ActionResult<ApiResponse<CounterDto>>> Status(long counterId, CancellationToken ct)
        => Ok(ApiResponse<CounterDto>.Ok(await _counter.GetCounterStatusAsync(counterId, ct)));

    // ─────────────── 櫃台 CRUD（§11.5） ───────────────

    /// <summary>列出櫃台（GET /api/queue/counters）</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CounterDto>>>> GetAll(
        [FromQuery] bool includeInactive = true,
        CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<CounterDto>>.Ok(await _crud.GetAllAsync(includeInactive, ct)));

    /// <summary>建立櫃台（POST /api/queue/counters）</summary>
    [HttpPost]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse<CounterDto>>> Create(
        [FromBody] UpsertCounterRequest request,
        CancellationToken ct)
        => Ok(ApiResponse<CounterDto>.Ok(await _crud.CreateAsync(request, ct), "建立成功"));

    /// <summary>更新櫃台（PUT /api/queue/counters/{id}）</summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse<CounterDto>>> Update(
        long id,
        [FromBody] UpsertCounterRequest request,
        CancellationToken ct)
        => Ok(ApiResponse<CounterDto>.Ok(await _crud.UpdateAsync(id, request, ct), "更新成功"));

    /// <summary>刪除櫃台（DELETE /api/queue/counters/{id}）</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken ct)
    {
        await _crud.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok(null, "刪除成功"));
    }
}

public class CompleteRequest
{
    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string? Remark { get; set; }
}

public class PauseRequest
{
    public bool Paused { get; set; } = true;
}
