using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Queue.Application.Abstractions;
using Queue.Application.Common;
using Queue.Application.Dtos;
using Queue.Domain.Common;
using Queue.Domain.Entities;
using Queue.Domain.Enums;
using Queue.Domain.Exceptions;

namespace Queue.Application.Services;

/// <summary>
/// 叫號引擎（§2.1 核心目標 / §5.4 櫃台 / §8.2 櫃台叫號）
/// Waiting → Calling → Serving → Completed
/// 排序：Priority DESC, CreatedAt ASC, Id ASC（§17.1）
/// </summary>
public class CounterService
{
    private readonly IQueueDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly WaitTimeEstimator _estimator;
    private readonly TicketHistoryWriter _history;
    private readonly DisplayQueryService _display;
    private readonly IQueueNotifier _notifier;
    private readonly ILogger<CounterService> _logger;

    public CounterService(
        IQueueDbContext db,
        IClock clock,
        ICurrentUser currentUser,
        WaitTimeEstimator estimator,
        TicketHistoryWriter history,
        DisplayQueryService display,
        IQueueNotifier notifier,
        ILogger<CounterService> logger)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _estimator = estimator;
        _history = history;
        _display = display;
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>
    /// 下一號：取出下一張 Waiting 票據並轉為 Calling。
    /// 使用 Row Lock 鎖住候選票，避免兩個櫃台同時叫到同一張票。
    /// </summary>
    public async Task<CallNextResultDto> CallNextAsync(long counterId, CancellationToken ct = default)
    {
        var counter = await GetCounterAsync(counterId, ct);

        if (counter.Status == QueueCounterStatus.Offline || !counter.IsActive)
        {
            throw new DomainException("COUNTER_UNAVAILABLE", "櫃台目前無法服務");
        }

        if (counter.Status == QueueCounterStatus.Paused)
        {
            throw new DomainException("COUNTER_PAUSED", "櫃台已暫停服務，請先恢復");
        }

        if (counter.CurrentTicketId.HasValue)
        {
            throw new DomainException("COUNTER_BUSY", $"櫃台 {counter.Code} 尚有服務中的票據，請先完成或轉移");
        }

        var now = _clock.UtcNow;
        var today = _clock.Today;

        var called = await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var serviceIdFilter = counter.ServiceId;

            // 以 raw SQL + FOR UPDATE SKIP LOCKED 挑出下一張票（安全且不會阻塞其他櫃台）
            // 注意：EF 的 SqlQuery<long> 會包成 SELECT s.Value FROM (...) s，故欄位需別名為 "Value"
            var candidateId = await _db.Database.SqlQuery<long>($"""
                SELECT t.id AS "Value"
                FROM queue_tickets t
                WHERE t.queue_date = {today}
                  AND t.status = {(int)QueueTicketStatus.Waiting}
                  AND ({serviceIdFilter} IS NULL OR t.service_id = {serviceIdFilter})
                ORDER BY t.priority DESC, t.created_at ASC, t.id ASC
                FOR UPDATE OF t SKIP LOCKED
                LIMIT 1
                """).FirstOrDefaultAsync(innerCt);

            if (candidateId == 0)
            {
                throw new DomainException("QUEUE_EMPTY", "目前沒有等待中的號碼");
            }

            var entity = await _db.Tickets.FirstAsync(t => t.Id == candidateId, innerCt);
            var from = entity.Status;

            QueueStatusTransitions.EnsureCanTransition(from, QueueTicketStatus.Calling);

            entity.Status = QueueTicketStatus.Calling;
            entity.CalledAt = now;
            entity.CounterId = counter.Id;
            entity.CallCount += 1;

            counter.Status = QueueCounterStatus.Busy;
            counter.CurrentTicketId = entity.Id;
            counter.UpdatedAt = now;

            _db.Histories.Add(_history.Write(entity.Id, from, QueueTicketStatus.Calling, QueueTicketAction.Call, counter.Id));
            await _db.SaveChangesAsync(innerCt);

            return entity;
        }, ct);

        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == called.ServiceId, ct);

        var notification = new TicketEventNotification
        {
            TicketId = called.Id,
            TicketNo = called.TicketNo,
            Status = called.Status.ToString(),
            Prefix = called.Prefix,
            ServiceId = called.ServiceId,
            ServiceName = service?.Name,
            ServiceCode = service?.Code,
            CounterId = counter.Id,
            CounterNo = counter.Code,
            Priority = called.Priority,
            Timestamp = now,
            Message = $"請 {called.TicketNo} 號至 {counter.Name} 櫃台"
        };

        await _notifier.TicketCalledAsync(notification, ct);
        await _notifier.DisplayUpdatedAsync(await _display.GetSnapshotAsync(ct), ct);

        _logger.LogInformation(
            "TicketCalled TicketId={TicketId} TicketNo={TicketNo} CounterId={CounterId} Operator={Operator}",
            called.Id, called.TicketNo, counter.Id, _currentUser.UserName);

        var nextWaiting = await _db.Tickets
            .AsNoTracking()
            .CountAsync(t => t.QueueDate == today
                             && t.ServiceId == called.ServiceId
                             && t.Status == QueueTicketStatus.Waiting, ct);

        return new CallNextResultDto
        {
            TicketId = called.Id,
            TicketNo = called.TicketNo,
            CounterId = counter.Id,
            CounterNo = counter.Code,
            Status = called.Status.ToString(),
            Position = nextWaiting + 1,
            PeopleAhead = nextWaiting,
            EstimatedMinutes = await _estimator.EstimateMinutesAsync(called, nextWaiting, ct),
            ServiceId = called.ServiceId,
            ServiceName = service?.Name
        };
    }

    /// <summary>再叫一次：Calling → Calling，累加 CallCount，超過上限自動改為 NoShow。</summary>
    public async Task<TicketStatusDto> RecallAsync(long counterId, CancellationToken ct = default)
    {
        var counter = await GetCounterAsync(counterId, ct);
        var ticket = await GetCurrentTicketAsync(counter, ct);

        QueueStatusTransitions.EnsureCanTransition(ticket.Status, QueueTicketStatus.Calling);

        var now = _clock.UtcNow;
        var from = ticket.Status;
        ticket.Status = QueueTicketStatus.Calling;
        ticket.CalledAt = now;
        ticket.CallCount += 1;

        var overLimit = ticket.CallCount > _display.MaxRecallCount;
        if (overLimit)
        {
            ticket.Status = QueueTicketStatus.NoShow;
            ticket.NoShowAt = now;
            counter.Status = QueueCounterStatus.Idle;
            counter.CurrentTicketId = null;
        }

        _db.Histories.Add(_history.Write(
            ticket.Id, from, ticket.Status,
            overLimit ? QueueTicketAction.NoShow : QueueTicketAction.Recall,
            counter.Id,
            overLimit ? $"超過再叫次數上限 {_display.MaxRecallCount}，自動轉為過號" : null));

        await _db.SaveChangesAsync(ct);

        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == ticket.ServiceId, ct);
        var notification = TicketService.BuildNotification(ticket, service, null, null);
        var withCounter = new TicketEventNotification
        {
            TicketId = notification.TicketId,
            TicketNo = notification.TicketNo,
            Status = notification.Status,
            Prefix = notification.Prefix,
            ServiceId = notification.ServiceId,
            ServiceName = notification.ServiceName,
            ServiceCode = notification.ServiceCode,
            CounterId = counter.Id,
            CounterNo = counter.Code,
            Priority = notification.Priority,
            Timestamp = now,
            Message = overLimit ? $"{ticket.TicketNo} 號多次未到，轉為過號" : $"請 {ticket.TicketNo} 號至 {counter.Name} 櫃台（再叫）"
        };

        if (overLimit)
        {
            await _notifier.TicketNoShowAsync(withCounter, ct);
        }
        else
        {
            await _notifier.TicketRecalledAsync(withCounter, ct);
        }

        await _notifier.DisplayUpdatedAsync(await _display.GetSnapshotAsync(ct), ct);

        return new TicketStatusDto
        {
            TicketId = ticket.Id,
            TicketNo = ticket.TicketNo,
            Status = ticket.Status.ToString(),
            CounterNo = counter.Code,
            CalledAt = ticket.CalledAt,
            UpdatedAt = now
        };
    }

    /// <summary>開始服務：Calling → Serving。</summary>
    public async Task<TicketStatusDto> StartAsync(long counterId, CancellationToken ct = default)
    {
        var counter = await GetCounterAsync(counterId, ct);
        var ticket = await GetCurrentTicketAsync(counter, ct);

        var from = ticket.Status;
        QueueStatusTransitions.EnsureCanTransition(from, QueueTicketStatus.Serving);

        var now = _clock.UtcNow;
        ticket.Status = QueueTicketStatus.Serving;
        ticket.ServingAt = now;
        counter.Status = QueueCounterStatus.Busy;
        counter.UpdatedAt = now;

        _db.Histories.Add(_history.Write(ticket.Id, from, QueueTicketStatus.Serving, QueueTicketAction.Start, counter.Id));
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("TicketStarted TicketId={TicketId} CounterId={CounterId}", ticket.Id, counter.Id);

        await Notify(ticket, counter, now, QueueTicketAction.Start, (s, n) => _notifier.TicketStartedAsync(n, ct), ct);
        return await BuildStatusAsync(ticket, counter, now, ct);
    }

    /// <summary>完成服務：Calling/Serving → Completed，並釋放櫃台。</summary>
    public async Task<TicketStatusDto> CompleteAsync(long counterId, string? remark, CancellationToken ct = default)
    {
        var counter = await GetCounterAsync(counterId, ct);
        var ticket = await GetCurrentTicketAsync(counter, ct);

        var from = ticket.Status;
        QueueStatusTransitions.EnsureCanTransition(from, QueueTicketStatus.Completed);

        var now = _clock.UtcNow;
        ticket.Status = QueueTicketStatus.Completed;
        ticket.CompletedAt = now;
        if (remark is not null)
        {
            ticket.Remark = remark;
        }

        counter.Status = QueueCounterStatus.Idle;
        counter.CurrentTicketId = null;
        counter.UpdatedAt = now;

        _db.Histories.Add(_history.Write(ticket.Id, from, QueueTicketStatus.Completed, QueueTicketAction.Complete, counter.Id, remark));
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "TicketCompleted TicketId={TicketId} TicketNo={TicketNo} CounterId={CounterId} ServiceMinutes={ServiceMinutes}",
            ticket.Id, ticket.TicketNo, counter.Id, ticket.ServiceMinutes);

        await Notify(ticket, counter, now, QueueTicketAction.Complete, (s, n) => _notifier.TicketCompletedAsync(n, ct), ct);
        return await BuildStatusAsync(ticket, counter, now, ct);
    }

    /// <summary>過號：Calling → NoShow，釋放櫃台。</summary>
    public async Task<TicketStatusDto> NoShowAsync(long counterId, string? remark, CancellationToken ct = default)
    {
        var counter = await GetCounterAsync(counterId, ct);
        var ticket = await GetCurrentTicketAsync(counter, ct);

        var from = ticket.Status;
        QueueStatusTransitions.EnsureCanTransition(from, QueueTicketStatus.NoShow);

        var now = _clock.UtcNow;
        ticket.Status = QueueTicketStatus.NoShow;
        ticket.NoShowAt = now;
        if (remark is not null)
        {
            ticket.Remark = remark;
        }

        counter.Status = QueueCounterStatus.Idle;
        counter.CurrentTicketId = null;
        counter.UpdatedAt = now;

        _db.Histories.Add(_history.Write(ticket.Id, from, QueueTicketStatus.NoShow, QueueTicketAction.NoShow, counter.Id, remark));
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("TicketNoShow TicketId={TicketId} CounterId={CounterId}", ticket.Id, counter.Id);

        await Notify(ticket, counter, now, QueueTicketAction.NoShow, (s, n) => _notifier.TicketNoShowAsync(n, ct), ct);
        return await BuildStatusAsync(ticket, counter, now, ct);
    }

    /// <summary>轉移：Serving/Calling → Transferred，票據回到 Waiting 佇列並指派目標櫃台。</summary>
    public async Task<TicketStatusDto> TransferAsync(long counterId, TransferRequest request, CancellationToken ct = default)
    {
        var counter = await GetCounterAsync(counterId, ct);
        var ticket = await GetCurrentTicketAsync(counter, ct);

        if (request.TargetCounterId == counterId)
        {
            throw new DomainException("TRANSFER_SAME_COUNTER", "目標櫃台不可為目前櫃台");
        }

        var target = await GetCounterAsync(request.TargetCounterId, ct);
        if (!target.IsActive || target.Status is QueueCounterStatus.Offline or QueueCounterStatus.Paused)
        {
            throw new DomainException("TARGET_COUNTER_UNAVAILABLE", "目標櫃台目前無法接受服務");
        }

        var from = ticket.Status;
        QueueStatusTransitions.EnsureCanTransition(from, QueueTicketStatus.Transferred);

        var now = _clock.UtcNow;

        counter.Status = QueueCounterStatus.Idle;
        counter.CurrentTicketId = null;
        counter.UpdatedAt = now;
        target.UpdatedAt = now;

        // 步驟一：標記為 Transferred，寫入歷程（§6 Serving → Transferred）
        ticket.Status = QueueTicketStatus.Transferred;
        ticket.CounterId = null;
        ticket.CalledAt = null;
        ticket.ServingAt = null;
        ticket.Remark = request.Remark ?? ticket.Remark;

        _db.Histories.Add(_history.Write(ticket.Id, from, QueueTicketStatus.Transferred, QueueTicketAction.Transfer,
            counter.Id, $"由 {counter.Code} 轉移至 {target.Code}：{request.Remark}"));

        // 步驟二：重新排回 Waiting 佇列（保留原取號時間以維持 FIFO 公平性與優先權）
        ticket.Status = QueueTicketStatus.Waiting;
        ticket.CalledAt = null;
        ticket.ServingAt = null;

        _db.Histories.Add(_history.Write(ticket.Id, QueueTicketStatus.Transferred, QueueTicketStatus.Waiting,
            QueueTicketAction.Resume, target.Id, "重新排入等待佇列"));

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "TicketTransferred TicketId={TicketId} From={FromCounter} To={ToCounter}",
            ticket.Id, counter.Code, target.Code);

        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == ticket.ServiceId, ct);
        var notification = new TicketEventNotification
        {
            TicketId = ticket.Id,
            TicketNo = ticket.TicketNo,
            Status = QueueTicketStatus.Transferred.ToString(),
            Prefix = ticket.Prefix,
            ServiceId = ticket.ServiceId,
            ServiceName = service?.Name,
            ServiceCode = service?.Code,
            CounterId = target.Id,
            CounterNo = target.Code,
            Priority = ticket.Priority,
            Timestamp = now,
            Message = $"{ticket.TicketNo} 號已轉移至 {target.Name} 櫃台，請重新排隊"
        };

        await _notifier.TicketTransferredAsync(notification, ct);
        await _notifier.DisplayUpdatedAsync(await _display.GetSnapshotAsync(ct), ct);

        return await BuildStatusAsync(ticket, target, now, ct);
    }

    /// <summary>暫停服務：櫃台狀態切換為 Paused，不影響既有票據。</summary>
    public async Task<CounterDto> PauseAsync(long counterId, bool paused, CancellationToken ct = default)
    {
        var counter = await GetCounterAsync(counterId, ct);
        var now = _clock.UtcNow;

        if (paused)
        {
            if (counter.CurrentTicketId.HasValue)
            {
                throw new DomainException("COUNTER_BUSY", "櫃台仍有服務中的票據，無法暫停");
            }

            counter.Status = QueueCounterStatus.Paused;
        }
        else
        {
            counter.Status = QueueCounterStatus.Idle;
        }

        counter.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("CounterPause CounterId={CounterId} Paused={Paused} Operator={Operator}",
            counter.Id, paused, _currentUser.UserName);

        await _notifier.DisplayUpdatedAsync(await _display.GetSnapshotAsync(ct), ct);
        return await CounterMappingService.MapAsync(_db, counter, _clock, ct);
    }

    /// <summary>取得櫃台目前狀態（供櫃台畫面輪詢/初始化）。</summary>
    public async Task<CounterDto> GetCounterStatusAsync(long counterId, CancellationToken ct = default)
    {
        var counter = await GetCounterAsync(counterId, ct);
        return await CounterMappingService.MapAsync(_db, counter, _clock, ct);
    }

    internal async Task<QueueCounter> GetCounterAsync(long counterId, CancellationToken ct)
    {
        return await _db.Counters.FirstOrDefaultAsync(c => c.Id == counterId, ct)
               ?? throw new DomainException("COUNTER_NOT_FOUND", "找不到櫃台");
    }

    private async Task<QueueTicket> GetCurrentTicketAsync(QueueCounter counter, CancellationToken ct)
    {
        if (!counter.CurrentTicketId.HasValue)
        {
            throw new DomainException("NO_CURRENT_TICKET", $"櫃台 {counter.Code} 目前沒有服務中的號碼");
        }

        return await _db.Tickets.FirstAsync(t => t.Id == counter.CurrentTicketId.Value, ct);
    }

    private async Task Notify(
        QueueTicket ticket,
        QueueCounter counter,
        DateTimeOffset now,
        QueueTicketAction action,
        Func<QueueTicket, TicketEventNotification, Task> publisher,
        CancellationToken ct)
    {
        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == ticket.ServiceId, ct);
        var baseNotification = TicketService.BuildNotification(ticket, service, null, null);
        var notification = new TicketEventNotification
        {
            TicketId = baseNotification.TicketId,
            TicketNo = baseNotification.TicketNo,
            Status = baseNotification.Status,
            Prefix = baseNotification.Prefix,
            ServiceId = baseNotification.ServiceId,
            ServiceName = baseNotification.ServiceName,
            ServiceCode = baseNotification.ServiceCode,
            CounterId = counter.Id,
            CounterNo = counter.Code,
            Priority = baseNotification.Priority,
            Timestamp = now,
            Message = action switch
            {
                QueueTicketAction.Start => $"{ticket.TicketNo} 號已於 {counter.Name} 開始服務",
                QueueTicketAction.Complete => $"{ticket.TicketNo} 號服務完成，請離場",
                QueueTicketAction.NoShow => $"{ticket.TicketNo} 號未到，轉為過號",
                _ => null
            }
        };

        await publisher(ticket, notification);
        await _notifier.DisplayUpdatedAsync(await _display.GetSnapshotAsync(ct), ct);
    }

    private static async Task<TicketStatusDto> BuildStatusAsync(QueueTicket ticket, QueueCounter counter, DateTimeOffset now, CancellationToken ct)
    {
        return new TicketStatusDto
        {
            TicketId = ticket.Id,
            TicketNo = ticket.TicketNo,
            Status = ticket.Status.ToString(),
            CounterNo = counter.Code,
            CalledAt = ticket.CalledAt,
            ServingAt = ticket.ServingAt,
            CompletedAt = ticket.CompletedAt,
            UpdatedAt = now
        };
    }
}
