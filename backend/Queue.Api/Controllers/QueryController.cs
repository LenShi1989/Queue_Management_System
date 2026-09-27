using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Queue.Api.Common;
using Queue.Application.Abstractions;
using Queue.Application.Dtos;
using Queue.Application.Services;
using Queue.Domain.Common;
using Queue.Domain.Enums;

namespace Queue.Api.Controllers;

/// <summary>
/// 查詢 / 顯示器（§11.3 Query API、§5.5 顯示器、§5.6 手機查詢）
/// </summary>
[ApiController]
[Route("api/queue")]
[ApiExplorerSettings(GroupName = SwaggerGroups.Display)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
public class QueryController : ControllerBase
{
    private readonly DisplayQueryService _query;
    private readonly StatisticsService _stats;

    public QueryController(DisplayQueryService query, StatisticsService stats)
    {
        _query = query;
        _stats = stats;
    }

    /// <summary>目前叫號（GET /api/queue/current）</summary>
    [HttpGet("current")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CurrentCallDto>>>> Current(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<CurrentCallDto>>.Ok(await _query.GetCurrentCallsAsync(ct)));

    /// <summary>等待佇列（GET /api/queue/waiting）</summary>
    [HttpGet("waiting")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WaitingTicketDto>>>> Waiting(
        [FromQuery] long? serviceId = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<WaitingTicketDto>>.Ok(await _query.GetWaitingAsync(serviceId, limit, ct)));

    /// <summary>叫號顯示器快照（GET /api/queue/display）</summary>
    [HttpGet("display")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<DisplaySnapshot>>> Display(CancellationToken ct)
        => Ok(ApiResponse<DisplaySnapshot>.Ok(await _query.GetSnapshotAsync(ct)));

    /// <summary>今日統計（GET /api/queue/statistics）</summary>
    [HttpGet("statistics")]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Statistics)]
    [Authorize(Roles = $"{QueueRoles.Admin},{QueueRoles.Manager}")]
    public async Task<ActionResult<ApiResponse<TodayStatisticsDto>>> Statistics(CancellationToken ct)
        => Ok(ApiResponse<TodayStatisticsDto>.Ok(await _stats.GetTodayAsync(ct)));

    /// <summary>尖峰時段（GET /api/queue/statistics/peak）</summary>
    [HttpGet("statistics/peak")]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Statistics)]
    [Authorize(Roles = $"{QueueRoles.Admin},{QueueRoles.Manager}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PeakHourDto>>>> Peak(
        [FromQuery] int days = 7,
        CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<PeakHourDto>>.Ok(await _stats.GetPeakHoursAsync(days, ct)));

    /// <summary>每日趨勢（GET /api/queue/statistics/trend）</summary>
    [HttpGet("statistics/trend")]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Statistics)]
    [Authorize(Roles = $"{QueueRoles.Admin},{QueueRoles.Manager}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DailyTrendDto>>>> Trend(
        [FromQuery] int days = 14,
        CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<DailyTrendDto>>.Ok(await _stats.GetDailyTrendAsync(days, ct)));
}

/// <summary>
/// 服務類型管理（§11.4 Service API、§5.1 服務類型管理）
/// </summary>
[ApiController]
[Route("api/queue/services")]
[ApiExplorerSettings(GroupName = SwaggerGroups.Service)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
public class ServicesController : ControllerBase
{
    private readonly QueueServiceTypeService _services;

    public ServicesController(QueueServiceTypeService services)
    {
        _services = services;
    }

    /// <summary>列出服務類型（GET /api/queue/services）</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ServiceDto>>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<ServiceDto>>.Ok(await _services.GetAllAsync(includeInactive, ct)));

    /// <summary>服務類型明細</summary>
    [HttpGet("{id:long}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ServiceDto>>> Get(long id, CancellationToken ct)
        => Ok(ApiResponse<ServiceDto>.Ok(await _services.GetAsync(id, ct)));

    /// <summary>建立服務類型（POST /api/queue/services）</summary>
    [HttpPost]
    [Authorize(Roles = $"{QueueRoles.Admin},{QueueRoles.Manager}")]
    public async Task<ActionResult<ApiResponse<ServiceDto>>> Create(
        [FromBody] UpsertServiceRequest request,
        CancellationToken ct)
        => Ok(ApiResponse<ServiceDto>.Ok(await _services.CreateAsync(request, ct), "建立成功"));

    /// <summary>更新服務類型（PUT /api/queue/services/{id}）</summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = $"{QueueRoles.Admin},{QueueRoles.Manager}")]
    public async Task<ActionResult<ApiResponse<ServiceDto>>> Update(
        long id,
        [FromBody] UpsertServiceRequest request,
        CancellationToken ct)
        => Ok(ApiResponse<ServiceDto>.Ok(await _services.UpdateAsync(id, request, ct), "更新成功"));

    /// <summary>刪除服務類型（DELETE /api/queue/services/{id}）</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Roles = QueueRoles.Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken ct)
    {
        await _services.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok(null, "刪除成功"));
    }
}

/// <summary>
/// 系統設定（§5.1 叫號/語音/顯示器設定、§40 環境變數）
/// </summary>
[ApiController]
[Route("api/queue/settings")]
[ApiExplorerSettings(GroupName = SwaggerGroups.Admin)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
public class SettingsController : ControllerBase
{
    private readonly QueueSettingService _settings;

    public SettingsController(QueueSettingService settings)
    {
        _settings = settings;
    }

    /// <summary>列出所有設定</summary>
    [HttpGet]
    [Authorize(Roles = $"{QueueRoles.Admin},{QueueRoles.Manager},{QueueRoles.Display},{QueueRoles.Counter}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SettingDto>>>> GetAll(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<SettingDto>>.Ok(await _settings.GetAllAsync(ct)));

    /// <summary>新增或更新設定（UPSERT）</summary>
    [HttpPut]
    [Authorize(Roles = $"{QueueRoles.Admin},{QueueRoles.Manager}")]
    public async Task<ActionResult<ApiResponse<object>>> Upsert([FromBody] UpsertSettingRequest request, CancellationToken ct)
    {
        await _settings.UpsertAsync(request, ct);
        return Ok(ApiResponse.Ok(null, "設定已更新"));
    }

    /// <summary>刪除設定</summary>
    [HttpDelete("{key}")]
    [Authorize(Roles = QueueRoles.Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string key, CancellationToken ct)
    {
        await _settings.DeleteAsync(key, ct);
        return Ok(ApiResponse.Ok(null, "刪除成功"));
    }
}
