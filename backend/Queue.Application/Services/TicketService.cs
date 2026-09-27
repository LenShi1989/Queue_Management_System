using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Queue.Application.Abstractions;
using Queue.Application.Common;
using Queue.Application.Dtos;
using Queue.Domain.Common;
using Queue.Domain.Entities;
using Queue.Domain.Enums;
using Queue.Domain.Exceptions;
using Queue.Domain.ValueObjects;

namespace Queue.Application.Services;

/// <summary>
/// 預估等待時間計算（§18 第一階段）
/// EstimatedWait = 前面人數 × 平均服務時間 ÷ 有效櫃台數
/// </summary>
public class WaitTimeEstimator
{
    private readonly IQueueDbContext _db;
    private readonly SettingProvider _settings;
    private readonly IClock _clock;
    private readonly ILogger<WaitTimeEstimator> _logger;

    public WaitTimeEstimator(IQueueDbContext db, SettingProvider settings, IClock clock, ILogger<WaitTimeEstimator> logger)
    {
        _db = db;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// 取得前面等待人數（含同優先權以上者，依 §17.1 排序規則計算）。
    /// </summary>
    public async Task<(int PeopleAhead, int? Position)> CountAheadAsync(QueueTicket ticket, CancellationToken ct)
    {
        var ahead = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.QueueDate == ticket.QueueDate
                        && t.ServiceId == ticket.ServiceId
                        && t.Status == QueueTicketStatus.Waiting
                        && (t.Priority > ticket.Priority
                            || (t.Priority == ticket.Priority && t.CreatedAt < ticket.CreatedAt)
                            || (t.Priority == ticket.Priority && t.CreatedAt == ticket.CreatedAt && t.Id < ticket.Id)))
            .CountAsync(ct);

        var position = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.QueueDate == ticket.QueueDate
                        && t.ServiceId == ticket.ServiceId
                        && t.Status == QueueTicketStatus.Waiting
                        && (t.Priority > ticket.Priority
                            || (t.Priority == ticket.Priority && t.CreatedAt <= ticket.CreatedAt)))
            .CountAsync(ct);

        return (ahead, position);
    }

    /// <summary>
    /// 計算預估等待分鐘數。
    /// </summary>
    public async Task<int> EstimateMinutesAsync(QueueTicket ticket, int peopleAhead, CancellationToken ct)
    {
        if (peopleAhead <= 0)
        {
            return 0;
        }

        var serviceMinutes = await _db.Services
            .AsNoTracking()
            .Where(s => s.Id == ticket.ServiceId)
            .Select(s => s.EstimatedServiceMinutes)
            .FirstOrDefaultAsync(ct);

        if (serviceMinutes <= 0)
        {
            serviceMinutes = (int)Math.Round(_settings.DefaultAverageServiceMinutes);
        }

        var effectiveCounters = await _db.Counters
            .AsNoTracking()
            .Where(c => c.IsActive
                        && (c.Status == QueueCounterStatus.Idle || c.Status == QueueCounterStatus.Busy)
                        && (c.ServiceId == null || c.ServiceId == ticket.ServiceId))
            .CountAsync(ct);

        if (effectiveCounters <= 0)
        {
            effectiveCounters = 1;
        }

        var estimate = (peopleAhead * (double)serviceMinutes) / effectiveCounters;
        return (int)Math.Ceiling(estimate);
    }
}

/// <summary>
/// 票據歷程記錄（§21 Audit / §44.3）
/// </summary>
public class TicketHistoryWriter
{
    private readonly IQueueDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public TicketHistoryWriter(IQueueDbContext db, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public QueueTicketHistory Write(
        long ticketId,
        QueueTicketStatus? from,
        QueueTicketStatus to,
        QueueTicketAction action,
        long? counterId = null,
        string? remark = null)
    {
        return new QueueTicketHistory
        {
            TicketId = ticketId,
            FromStatus = from,
            ToStatus = to,
            Action = action,
            CounterId = counterId,
            OperatorId = _currentUser.UserId,
            OperatorName = _currentUser.DisplayName ?? _currentUser.UserName,
            IpAddress = _currentUser.IpAddress,
            UserAgent = Truncate(_currentUser.UserAgent, 400),
            Remark = Truncate(remark, 400),
            CreatedAt = _clock.UtcNow
        };
    }

    private static string? Truncate(string? value, int max)
        => value is null ? null : value.Length <= max ? value : value[..max];
}

/// <summary>
/// 取號 / 票據查詢 / 取消（§5.2、§11.1）
/// </summary>
public class TicketService
{
    private readonly IQueueDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly TicketNumberGenerator _numberGenerator;
    private readonly WaitTimeEstimator _estimator;
    private readonly TicketHistoryWriter _history;
    private readonly QrTokenService _qr;
    private readonly IQueueNotifier _notifier;
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        IQueueDbContext db,
        IClock clock,
        ICurrentUser currentUser,
        TicketNumberGenerator numberGenerator,
        WaitTimeEstimator estimator,
        TicketHistoryWriter history,
        QrTokenService qr,
        IQueueNotifier notifier,
        ILogger<TicketService> logger)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _numberGenerator = numberGenerator;
        _estimator = estimator;
        _history = history;
        _qr = qr;
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>
    /// 建立票據（取號）。整個流程在單一交易內：鎖定序列列 → 取號 → 寫入票據與歷程。
    /// </summary>
    public async Task<TicketDto> CreateAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        var service = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ServiceId, ct)
            ?? throw new DomainException("SERVICE_NOT_FOUND", "找不到服務類型");

        if (!service.IsActive)
        {
            throw new DomainException("SERVICE_INACTIVE", "此服務類型已停用");
        }

        var today = _clock.Today;
        var now = _clock.UtcNow;
        var signedToken = string.Empty;

        var ticket = await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var (sequenceNo, ticketNo) = await _numberGenerator.NextAsync(service, today, innerCt);

            var entity = new QueueTicket
            {
                QueueDate = today,
                TicketNo = ticketNo,
                Prefix = service.Prefix,
                SequenceNo = sequenceNo,
                ServiceId = service.Id,
                Status = QueueTicketStatus.Waiting,
                Priority = service.Priority + request.Priority,
                CustomerName = request.CustomerName,
                CustomerPhone = request.CustomerPhone,
                Remark = request.Remark,
                CreatedAt = now
            };

            _db.Tickets.Add(entity);
            await _db.SaveChangesAsync(innerCt);

            // 取得 DB 產生的 Id 後才簽發 QR token（避免 token 內含猜測得到的 ID）
            signedToken = _qr.Create(entity.Id, out var qrHash);
            entity.QrTokenHash = qrHash;

            _db.Histories.Add(_history.Write(entity.Id, null, QueueTicketStatus.Waiting, QueueTicketAction.Create, remark: request.Remark));
            await _db.SaveChangesAsync(innerCt);

            return entity;
        }, ct);

        _logger.LogInformation(
            "TicketCreated TicketId={TicketId} TicketNo={TicketNo} ServiceId={ServiceId} Priority={Priority} Operator={Operator}",
            ticket.Id, ticket.TicketNo, ticket.ServiceId, ticket.Priority, _currentUser.UserName);

        // 推播與統計在交易外進行，避免持有 row lock 時呼叫外部資源
        var (peopleAhead, position) = await _estimator.CountAheadAsync(ticket, ct);
        var estimated = await _estimator.EstimateMinutesAsync(ticket, peopleAhead, ct);

        await _notifier.TicketCreatedAsync(BuildNotification(ticket, service, position, estimated), ct);

        return Map(ticket, service, position, peopleAhead, estimated, signedToken);
    }

    public async Task<TicketDto> GetAsync(long ticketId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Service)
            .Include(t => t.Counter)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new DomainException("QUEUE_NOT_FOUND", "找不到排隊號碼");

        return await MapAsync(ticket, ct);
    }

    public async Task<TicketDto> GetByQrTokenAsync(string token, CancellationToken ct = default)
    {
        if (!_qr.TryValidate(token, out var ticketId))
        {
            throw new DomainException("QR_TOKEN_INVALID", "QR Code 連結無效或已過期");
        }

        var ticket = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Service)
            .Include(t => t.Counter)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new DomainException("QUEUE_NOT_FOUND", "找不到排隊號碼");

        var dto = await MapAsync(ticket, ct);
        dto.QrToken = token;
        dto.QrUrl = _qr.CreateUrl(token);
        return dto;
    }

    public async Task<TicketStatusDto> GetStatusAsync(long ticketId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Service)
            .Include(t => t.Counter)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new DomainException("QUEUE_NOT_FOUND", "找不到排隊號碼");

        return await MapStatusAsync(ticket, ct);
    }

    public async Task<PagedResult<TicketDto>> QueryAsync(TicketQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var date = query.Date ?? _clock.Today;

        IQueryable<QueueTicket> q = _db.Tickets
            .AsNoTracking()
            .Include(t => t.Service)
            .Include(t => t.Counter)
            .Where(t => t.QueueDate == date);

        if (query.ServiceId.HasValue)
        {
            q = q.Where(t => t.ServiceId == query.ServiceId.Value);
        }

        if (query.CounterId.HasValue)
        {
            q = q.Where(t => t.CounterId == query.CounterId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<QueueTicketStatus>(query.Status, ignoreCase: true, out var status))
            {
                throw new DomainException("INVALID_STATUS", $"無效的狀態值：{query.Status}");
            }

            q = q.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.TicketNo))
        {
            var no = query.TicketNo.Trim().ToUpperInvariant();
            q = q.Where(t => t.TicketNo == no);
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = new List<TicketDto>(items.Count);
        foreach (var t in items)
        {
            dtos.Add(await MapAsync(t, ct));
        }

        return PagedResult<TicketDto>.Create(dtos, total, page, pageSize);
    }

    public async Task<IReadOnlyList<TicketHistoryDto>> GetHistoryAsync(long ticketId, CancellationToken ct = default)
    {
        if (!await _db.Tickets.AsNoTracking().AnyAsync(t => t.Id == ticketId, ct))
        {
            throw new DomainException("QUEUE_NOT_FOUND", "找不到排隊號碼");
        }

        return await _db.Histories
            .AsNoTracking()
            .Where(h => h.TicketId == ticketId)
            .OrderBy(h => h.Id)
            .Select(h => new TicketHistoryDto
            {
                Id = h.Id,
                TicketId = h.TicketId,
                FromStatus = h.FromStatus.HasValue ? h.FromStatus.Value.ToString() : null,
                ToStatus = h.ToStatus.ToString(),
                Action = h.Action.ToString(),
                CounterId = h.CounterId,
                OperatorName = h.OperatorName,
                CreatedAt = h.CreatedAt,
                IpAddress = h.IpAddress,
                Remark = h.Remark
            })
            .ToListAsync(ct);
    }

    /// <summary>取消票據：僅 Waiting 可取消（§6 Waiting → Cancelled）。</summary>
    public async Task<TicketDto> CancelAsync(long ticketId, string? remark, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new DomainException("QUEUE_NOT_FOUND", "找不到排隊號碼");

        QueueStatusTransitions.EnsureCanTransition(ticket.Status, QueueTicketStatus.Cancelled);

        var from = ticket.Status;
        ticket.Status = QueueTicketStatus.Cancelled;
        ticket.CancelledAt = _clock.UtcNow;
        ticket.Remark = remark ?? ticket.Remark;

        _db.Histories.Add(_history.Write(ticket.Id, from, QueueTicketStatus.Cancelled, QueueTicketAction.Cancel, ticket.CounterId, remark));
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("TicketCancelled TicketId={TicketId} TicketNo={TicketNo} Operator={Operator}", ticket.Id, ticket.TicketNo, _currentUser.UserName);

        var service = await _db.Services.AsNoTracking().FirstAsync(s => s.Id == ticket.ServiceId, ct);
        await _notifier.TicketCancelledAsync(BuildNotification(ticket, service, null, null), ct);
        await _notifier.DisplayUpdatedAsync(await new DisplayQueryService(_db, _clock).GetSnapshotAsync(ct), ct);

        return await GetAsync(ticketId, ct);
    }

    private async Task<TicketDto> MapAsync(QueueTicket ticket, CancellationToken ct)
    {
        var (peopleAhead, position) = QueueStatusTransitions.IsInQueue(ticket.Status)
            ? await _estimator.CountAheadAsync(ticket, ct)
            : (0, (int?)null);

        var estimated = peopleAhead > 0
            ? await _estimator.EstimateMinutesAsync(ticket, peopleAhead, ct)
            : 0;

        return Map(ticket, ticket.Service, position, peopleAhead, estimated, null);
    }

    private async Task<TicketStatusDto> MapStatusAsync(QueueTicket ticket, CancellationToken ct)
    {
        var (peopleAhead, position) = QueueStatusTransitions.IsInQueue(ticket.Status)
            ? await _estimator.CountAheadAsync(ticket, ct)
            : (0, (int?)null);

        var estimated = peopleAhead > 0
            ? await _estimator.EstimateMinutesAsync(ticket, peopleAhead, ct)
            : 0;

        var currentCalling = await CurrentCallingNoAsync(ticket, ct);

        return new TicketStatusDto
        {
            TicketId = ticket.Id,
            TicketNo = ticket.TicketNo,
            Status = ticket.Status.ToString(),
            CurrentCallingNo = currentCalling,
            CounterNo = ticket.Counter?.Code,
            Position = position,
            PeopleAhead = peopleAhead,
            EstimatedMinutes = estimated,
            CreatedAt = ticket.CreatedAt,
            CalledAt = ticket.CalledAt,
            ServingAt = ticket.ServingAt,
            CompletedAt = ticket.CompletedAt,
            UpdatedAt = ticket.CompletedAt ?? ticket.CalledAt ?? ticket.CreatedAt
        };
    }

    private async Task<string?> CurrentCallingNoAsync(QueueTicket ticket, CancellationToken ct)
    {
        var counterId = ticket.CounterId;
        if (counterId is null && ticket.Status == QueueTicketStatus.Waiting)
        {
            var activeCounter = await _db.Counters
                .AsNoTracking()
                .Where(c => c.IsActive && c.Status != QueueCounterStatus.Offline
                            && (c.ServiceId == null || c.ServiceId == ticket.ServiceId))
                .Select(c => (long?)c.Id)
                .FirstOrDefaultAsync(ct);
            counterId = activeCounter;
        }

        if (counterId is null)
        {
            return null;
        }

        return await _db.Tickets
            .AsNoTracking()
            .Where(t => t.CounterId == counterId
                        && (t.Status == QueueTicketStatus.Calling || t.Status == QueueTicketStatus.Serving))
            .OrderByDescending(t => t.CalledAt)
            .Select(t => t.TicketNo)
            .FirstOrDefaultAsync(ct);
    }

    private TicketDto Map(
        QueueTicket ticket,
        QueueService? service,
        int? position,
        int peopleAhead,
        int estimated,
        string? qrToken)
    {
        return new TicketDto
        {
            TicketId = ticket.Id,
            Id = ticket.Id,
            TicketNo = ticket.TicketNo,
            Status = ticket.Status.ToString(),
            QueueDate = ticket.QueueDate,
            Prefix = ticket.Prefix,
            SequenceNo = ticket.SequenceNo,
            ServiceId = ticket.ServiceId,
            ServiceName = service?.Name ?? string.Empty,
            CounterId = ticket.CounterId,
            CounterNo = ticket.Counter?.Code,
            Priority = ticket.Priority,
            Position = position,
            PeopleAhead = peopleAhead,
            EstimatedMinutes = estimated,
            CustomerName = ticket.CustomerName,
            CustomerPhone = ticket.CustomerPhone,
            QrToken = qrToken,
            QrUrl = qrToken is null ? null : _qr.CreateUrl(qrToken),
            CreatedAt = ticket.CreatedAt,
            CalledAt = ticket.CalledAt,
            ServingAt = ticket.ServingAt,
            CompletedAt = ticket.CompletedAt,
            WaitingMinutes = ticket.CalledAt.HasValue ? Math.Round(ticket.WaitingMinutes, 1) : null,
            ServiceMinutes = ticket.ServiceMinutes is null ? null : Math.Round(ticket.ServiceMinutes.Value, 1),
            CallCount = ticket.CallCount,
            Remark = ticket.Remark
        };
    }

    internal static TicketEventNotification BuildNotification(
        QueueTicket ticket,
        QueueService? service,
        int? position,
        int? estimated)
    {
        return new TicketEventNotification
        {
            TicketId = ticket.Id,
            TicketNo = ticket.TicketNo,
            Status = ticket.Status.ToString(),
            Prefix = ticket.Prefix,
            ServiceId = ticket.ServiceId,
            ServiceName = service?.Name,
            ServiceCode = service?.Code,
            CounterId = ticket.CounterId,
            Position = position,
            EstimatedMinutes = estimated,
            Priority = ticket.Priority,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
