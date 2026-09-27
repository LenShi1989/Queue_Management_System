using Microsoft.EntityFrameworkCore;
using Queue.Application.Abstractions;
using Queue.Application.Dtos;
using Queue.Domain.Common;
using Queue.Domain.Entities;
using Queue.Domain.Enums;

namespace Queue.Application.Services;

/// <summary>
/// 顯示器 / 查詢（§5.5 顯示器、§5.6 手機查詢、§11.3 Query API）
/// </summary>
public class DisplayQueryService
{
    private const int RecentCallCount = 10;
    private const int WaitingPreviewPerCounter = 5;

    private readonly IQueueDbContext _db;
    private readonly IClock _clock;

    public DisplayQueryService(IQueueDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public int MaxRecallCount { get; private set; } = 2;

    /// <summary>由 DI 初始化時注入設定（避免額外 DB 查詢散落各處）。</summary>
    public void Configure(int maxRecallCount) => MaxRecallCount = Math.Max(0, maxRecallCount);

    /// <summary>叫號大螢幕快照（§10.4）。</summary>
    public async Task<DisplaySnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var today = _clock.Today;
        var now = _clock.UtcNow;

        var counters = await _db.Counters
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.Code,
                c.Name,
                c.Status,
                c.ServiceId,
                Current = c.CurrentTicket == null
                    ? null
                    : new DisplayTicket
                    {
                        TicketId = c.CurrentTicket.Id,
                        TicketNo = c.CurrentTicket.TicketNo,
                        Status = c.CurrentTicket.Status.ToString(),
                        CalledAt = c.CurrentTicket.CalledAt,
                        Timestamp = now
                    }
            })
            .ToListAsync(ct);

        var counterIds = counters.Select(c => c.Id).ToList();

        var waitingByCounter = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.QueueDate == today
                        && t.Status == QueueTicketStatus.Waiting
                        && t.CounterId != null
                        && counterIds.Contains(t.CounterId.Value))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .Select(t => new { t.CounterId, t.Id, t.TicketNo, t.Status, t.CalledAt })
            .ToListAsync(ct);

        var serviceStates = await _db.Services
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Prefix,
                WaitingCount = _db.Tickets.Count(t => t.QueueDate == today && t.ServiceId == s.Id && t.Status == QueueTicketStatus.Waiting),
                ServingCount = _db.Tickets.Count(t => t.QueueDate == today && t.ServiceId == s.Id && t.Status == QueueTicketStatus.Serving),
                CurrentNo = _db.Tickets
                    .Where(t => t.QueueDate == today && t.ServiceId == s.Id && t.Status == QueueTicketStatus.Calling)
                    .OrderByDescending(t => t.CalledAt)
                    .Select(t => t.TicketNo)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var recentCalls = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.QueueDate == today && t.CalledAt != null)
            .OrderByDescending(t => t.CalledAt)
            .Take(RecentCallCount)
            .Select(t => new DisplayTicket
            {
                TicketId = t.Id,
                TicketNo = t.TicketNo,
                Status = t.Status.ToString(),
                CalledAt = t.CalledAt,
                Timestamp = t.CalledAt!.Value
            })
            .ToListAsync(ct);

        var waitingTotal = serviceStates.Sum(s => s.WaitingCount);
        var waitingPriority = await _db.Tickets
            .AsNoTracking()
            .CountAsync(t => t.QueueDate == today && t.Status == QueueTicketStatus.Waiting && t.Priority > 0, ct);

        var counterStates = counters.Select(c => new DisplayCounterState
        {
            CounterId = c.Id,
            CounterNo = c.Code,
            CounterName = c.Name,
            Status = c.Status.ToString(),
            Current = c.Current,
            Waiting = waitingByCounter
                .Where(w => w.CounterId == c.Id)
                .Take(WaitingPreviewPerCounter)
                .Select(w => new DisplayTicket
                {
                    TicketId = w.Id,
                    TicketNo = w.TicketNo,
                    Status = QueueTicketStatus.Waiting.ToString(),
                    CalledAt = null,
                    Timestamp = now
                })
                .ToList()
        }).ToList();

        return new DisplaySnapshot
        {
            Timestamp = now,
            WaitingTotal = waitingTotal,
            WaitingTotalWithPriority = waitingPriority,
            Counters = counterStates,
            Services = serviceStates.Select(s => new DisplayServiceState
            {
                ServiceId = s.Id,
                ServiceName = s.Name,
                Prefix = s.Prefix,
                WaitingCount = s.WaitingCount,
                ServingCount = s.ServingCount,
                CurrentTicketNo = s.CurrentNo
            }).ToList(),
            RecentCalls = recentCalls
        };
    }

    /// <summary>目前叫號（§11.3 GET /api/queue/current）。</summary>
    public async Task<IReadOnlyList<CurrentCallDto>> GetCurrentCallsAsync(CancellationToken ct = default)
    {
        var today = _clock.Today;

        return await _db.Tickets
            .AsNoTracking()
            .Where(t => t.QueueDate == today && t.Status == QueueTicketStatus.Calling)
            .OrderBy(t => t.CalledAt)
            .Select(t => new CurrentCallDto
            {
                TicketId = t.Id,
                TicketNo = t.TicketNo,
                ServiceName = t.Service!.Name,
                CounterId = t.CounterId,
                CounterNo = t.Counter!.Code,
                CalledAt = t.CalledAt,
                Timestamp = t.CalledAt!.Value
            })
            .ToListAsync(ct);
    }

    /// <summary>等待佇列（§11.3 GET /api/queue/waiting）。</summary>
    public async Task<IReadOnlyList<WaitingTicketDto>> GetWaitingAsync(long? serviceId, int limit, CancellationToken ct = default)
    {
        var today = _clock.Today;
        var take = Math.Clamp(limit, 1, 200);

        var query = _db.Tickets
            .AsNoTracking()
            .Where(t => t.QueueDate == today && t.Status == QueueTicketStatus.Waiting);

        if (serviceId.HasValue)
        {
            query = query.Where(t => t.ServiceId == serviceId.Value);
        }

        var items = await query
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Take(take)
            .Select(t => new WaitingTicketDto
            {
                TicketId = t.Id,
                TicketNo = t.TicketNo,
                ServiceName = t.Service!.Name,
                Priority = t.Priority,
                CreatedAt = t.CreatedAt,
                WaitingMinutes = 0d
            })
            .ToListAsync(ct);

        var now = _clock.UtcNow;
        return items
            .Select((t, i) =>
            {
                t.Position = i + 1;
                t.WaitingMinutes = Math.Round((now - t.CreatedAt).TotalMinutes, 1);
                return t;
            })
            .ToList();
    }
}

public class CurrentCallDto
{
    public long TicketId { get; set; }

    public string TicketNo { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public long? CounterId { get; set; }

    public string? CounterNo { get; set; }

    public DateTimeOffset? CalledAt { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}

public class WaitingTicketDto
{
    public long TicketId { get; set; }

    public string TicketNo { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public int Priority { get; set; }

    public int Position { get; set; }

    public double WaitingMinutes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
