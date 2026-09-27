using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Queue.Api.Common;
using Queue.Application.Dtos;
using Queue.Application.Services;
using Queue.Domain.Common;
using Queue.Domain.Enums;

namespace Queue.Api.Controllers;

/// <summary>
/// 取號 / 票據查詢（§11.1 Ticket API、§5.2 取號）
/// 取號與查詢允許匿名（Kiosk / 手機），取消與明細需登入。
/// </summary>
[ApiController]
[Route("api/queue/tickets")]
[ApiExplorerSettings(GroupName = SwaggerGroups.Queue)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
public class TicketsController : ControllerBase
{
    private readonly TicketService _tickets;

    public TicketsController(TicketService tickets)
    {
        _tickets = tickets;
    }

    /// <summary>取號（POST /api/queue/tickets）</summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TicketDto>>> Create(
        [FromBody] CreateTicketRequest request,
        CancellationToken ct)
    {
        var result = await _tickets.CreateAsync(request, ct);
        return Ok(ApiResponse<TicketDto>.Ok(result, $"取號成功：{result.TicketNo}"));
    }

    /// <summary>票據明細（GET /api/queue/tickets/{id}）</summary>
    [HttpGet("{id:long}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TicketDto>>> Get(long id, CancellationToken ct)
        => Ok(ApiResponse<TicketDto>.Ok(await _tickets.GetAsync(id, ct)));

    /// <summary>即時進度（GET /api/queue/tickets/{id}/status）</summary>
    [HttpGet("{id:long}/status")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TicketStatusDto>>> GetStatus(long id, CancellationToken ct)
        => Ok(ApiResponse<TicketStatusDto>.Ok(await _tickets.GetStatusAsync(id, ct)));

    /// <summary>取消票據（POST /api/queue/tickets/{id}/cancel）</summary>
    [HttpPost("{id:long}/cancel")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TicketDto>>> Cancel(long id, [FromBody] CancelTicketRequest? request, CancellationToken ct)
        => Ok(ApiResponse<TicketDto>.Ok(await _tickets.CancelAsync(id, request?.Remark, ct), "已取消"));

    /// <summary>票據歷程（Audit Log）</summary>
    [HttpGet("{id:long}/history")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TicketHistoryDto>>>> History(long id, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<TicketHistoryDto>>.Ok(await _tickets.GetHistoryAsync(id, ct)));

    /// <summary>票據查詢（管理/櫃台）</summary>
    [HttpGet]
    [Authorize(Roles = $"{QueueRoles.Admin},{QueueRoles.Manager},{QueueRoles.Counter}")]
    public async Task<ActionResult<ApiResponse<PagedResult<TicketDto>>>> Query(
        [FromQuery] TicketQuery query,
        CancellationToken ct)
        => Ok(ApiResponse<PagedResult<TicketDto>>.Ok(await _tickets.QueryAsync(query, ct)));
}

public class CancelTicketRequest
{
    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string? Remark { get; set; }
}
