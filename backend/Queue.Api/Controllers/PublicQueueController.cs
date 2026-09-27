using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Queue.Api.Common;
using Queue.Application.Dtos;
using Queue.Application.Services;
using Queue.Domain.Common;
using Queue.Domain.Enums;

namespace Queue.Api.Controllers;

/// <summary>
/// 公開查詢（§24 QR Code 手機查詢：GET /api/public/queue/{token}）
/// 以簽章 Token 驗證，不暴露資料庫 ID，並具備 Rate Limit（§25）。
/// </summary>
[ApiController]
[Route("api/public/queue")]
[ApiExplorerSettings(GroupName = SwaggerGroups.Queue)]
[AllowAnonymous]
[Produces("application/json")]
[EnableRateLimiting("public-query")]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status429TooManyRequests)]
public class PublicQueueController : ControllerBase
{
    private readonly TicketService _tickets;

    public PublicQueueController(TicketService tickets)
    {
        _tickets = tickets;
    }

    /// <summary>以 QR Token 查詢票據進度</summary>
    [HttpGet("{token}")]
    public async Task<ActionResult<ApiResponse<TicketStatusDto>>> GetByToken(string token, CancellationToken ct)
    {
        var ticket = await _tickets.GetByQrTokenAsync(token, ct);
        return Ok(ApiResponse<TicketStatusDto>.Ok(new TicketStatusDto
        {
            TicketId = ticket.TicketId,
            TicketNo = ticket.TicketNo,
            Status = ticket.Status,
            CounterNo = ticket.CounterNo,
            Position = ticket.Position,
            PeopleAhead = ticket.PeopleAhead,
            EstimatedMinutes = ticket.EstimatedMinutes,
            CreatedAt = ticket.CreatedAt,
            CalledAt = ticket.CalledAt,
            ServingAt = ticket.ServingAt,
            CompletedAt = ticket.CompletedAt,
            UpdatedAt = ticket.CompletedAt ?? ticket.CalledAt ?? ticket.CreatedAt
        }));
    }

    /// <summary>以 QR Token 取得票據明細（含 QR Url，供列印/重新顯示）</summary>
    [HttpGet("{token}/detail")]
    public async Task<ActionResult<ApiResponse<TicketDto>>> GetDetail(string token, CancellationToken ct)
        => Ok(ApiResponse<TicketDto>.Ok(await _tickets.GetByQrTokenAsync(token, ct)));
}
