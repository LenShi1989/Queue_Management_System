using Microsoft.EntityFrameworkCore;
using Queue.Application.Abstractions;
using Queue.Application.Dtos;
using Queue.Domain.Common;
using Queue.Domain.Enums;

namespace Queue.Application.Services;

/// <summary>
/// 統計分析（§5.7 / §22 Dashboard / 報表）
/// </summary>
public class StatisticsService
{
    private readonly IQueueDbContext _db;
    private readonly IClock _clock;

    public StatisticsService(IQueueDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<TodayStatisticsDto> GetTodayAsync(CancellationToken ct = default)
    {
        var date = _clock.Today;
        var tickets = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.QueueDate == date)
            .Select(t => new
            {
                t.Id,
                t.ServiceId,
                t.CounterId,
                t.Status,
                t.CreatedAt,
                t.CalledAt,
                t.ServingAt,
                t.CompletedAt,
                t.NoShowAt,
                t.CancelledAt
            })
            .ToListAsync(ct);

        var counters = await _db.Counters.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.Code, c.Name, c.Status })
            .ToListAsync(ct);

        var services = await _db.Services.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new { s.Id, s.Name, s.Prefix })
            .ToListAsync(ct);

        var completed = tickets.Where(t => t.Status == QueueTicketStatus.Completed).ToList();
        var waiting = tickets.Count(t => t.Status == QueueTicketStatus.Waiting);
        var calling = tickets.Count(t => t.Status == QueueTicketStatus.Calling);
        var serving = tickets.Count(t => t.Status == QueueTicketStatus.Serving);
        var noShow = tickets.Count(t => t.Status == QueueTicketStatus.NoShow);
        var cancelled = tickets.Count(t => t.Status == QueueTicketStatus.Cancelled);
        var transferred = tickets.Count(t => t.Status == QueueTicketStatus.Transferred);

        var avgWaiting = tickets
            .Where(t => t.CalledAt.HasValue)
            .Select(t => (t.CalledAt!.Value - t.CreatedAt).TotalMinutes)
            .DefaultIfEmpty(0)
            .Average();

        var avgService = completed
            .Where(t => t.ServingAt.HasValue)
            .Select(t => (t.CompletedAt!.Value - t.ServingAt!.Value).TotalMinutes)
            .DefaultIfEmpty(0)
            .Average();

        var avgTotal = completed
            .Where(t => t.CompletedAt.HasValue)
            .Select(t => (t.CompletedAt!.Value - t.CreatedAt).TotalMinutes)
            .DefaultIfEmpty(0)
            .Average();

        var totalTickets = tickets.Count;

        var serviceStats = services.Select(s =>
        {
            var rows = tickets.Where(t => t.ServiceId == s.Id).ToList();
            var comp = rows.Where(t => t.Status == QueueTicketStatus.Completed && t.ServingAt.HasValue).ToList();
            return new ServiceStatisticDto
            {
                ServiceId = s.Id,
                ServiceName = s.Name,
                Prefix = s.Prefix,
                Total = rows.Count,
                Completed = rows.Count(t => t.Status == QueueTicketStatus.Completed),
                Waiting = rows.Count(t => t.Status == QueueTicketStatus.Waiting),
                NoShow = rows.Count(t => t.Status == QueueTicketStatus.NoShow),
                Cancelled = rows.Count(t => t.Status == QueueTicketStatus.Cancelled),
                AverageServiceMinutes = Round1(comp
                    .Select(t => (t.CompletedAt!.Value - t.ServingAt!.Value).TotalMinutes)
                    .DefaultIfEmpty(0)
                    .Average())
            };
        }).ToList();

        var counterStats = counters.Select(c =>
        {
            var called = tickets.Where(t => t.CounterId == c.Id && t.CalledAt.HasValue).ToList();
            var comp = called.Where(t => t.Status == QueueTicketStatus.Completed && t.ServingAt.HasValue).ToList();
            return new CounterStatisticDto
            {
                CounterId = c.Id,
                CounterNo = c.Code,
                CounterName = c.Name,
                CalledCount = called.Count,
                CompletedCount = comp.Count,
                NoShowCount = called.Count(t => t.Status == QueueTicketStatus.NoShow),
                AverageServiceMinutes = Round1(comp
                    .Select(t => (t.CompletedAt!.Value - t.ServingAt!.Value).TotalMinutes)
                    .DefaultIfEmpty(0)
                    .Average())
            };
        }).ToList();

        var tz = ResolveTimeZone();

        var hourly = Enumerable.Range(0, 24).Select(hour =>
        {
            var rows = tickets.Where(t => TimeZoneInfo.ConvertTime(t.CreatedAt, tz).Hour == hour).ToList();
            return new HourlyStatisticDto
            {
                Hour = hour,
                Total = rows.Count,
                Completed = rows.Count(t => t.Status == QueueTicketStatus.Completed),
                NoShow = rows.Count(t => t.Status == QueueTicketStatus.NoShow),
                Cancelled = rows.Count(t => t.Status == QueueTicketStatus.Cancelled)
            };
        }).ToList();

        return new TodayStatisticsDto
        {
            Date = date,
            TotalTickets = totalTickets,
            CompletedCount = completed.Count,
            WaitingCount = waiting,
            CallingCount = calling,
            ServingCount = serving,
            NoShowCount = noShow,
            CancelledCount = cancelled,
            TransferredCount = transferred,
            AverageWaitingMinutes = Round1(avgWaiting),
            AverageServiceMinutes = Round1(avgService),
            AverageTotalMinutes = Round1(avgTotal),
            NoShowRate = totalTickets == 0 ? 0 : Round1(noShow * 100d / totalTickets),
            CancelRate = totalTickets == 0 ? 0 : Round1(cancelled * 100d / totalTickets),
            ActiveCounters = counters.Count(c => c.Status != QueueCounterStatus.Offline),
            IdleCounters = counters.Count(c => c.Status == QueueCounterStatus.Idle),
            Services = serviceStats,
            Counters = counterStats,
            Hourly = hourly
        };
    }

    /// <summary>尖峰時段（§22 報表）。</summary>
    public async Task<IReadOnlyList<PeakHourDto>> GetPeakHoursAsync(int days, CancellationToken ct = default)
    {
        var from = _clock.Today.AddDays(-Math.Clamp(days, 1, 90) + 1);
        var tz = ResolveTimeZone();

        var rows = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.QueueDate >= from)
            .Select(t => t.CreatedAt)
            .ToListAsync(ct);

        var buckets = new int[24];
        foreach (var created in rows)
        {
            buckets[TimeZoneInfo.ConvertTime(created, tz).Hour]++;
        }

        var total = rows.Count;
        return Enumerable.Range(0, 24)
            .Select(h => new PeakHourDto
            {
                Hour = h,
                Total = buckets[h],
                Ratio = total == 0 ? 0 : Math.Round(buckets[h] * 100d / total, 2)
            })
            .OrderByDescending(p => p.Total)
            .ThenBy(p => p.Hour)
            .ToList();
    }

    /// <summary>每日趨勢（§22 每日取號量）。</summary>
    public async Task<IReadOnlyList<DailyTrendDto>> GetDailyTrendAsync(int days, CancellationToken ct = default)
    {
        var take = Math.Clamp(days, 1, 90);
        var today = _clock.Today;
        var from = today.AddDays(-(take - 1));

        var rows = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.QueueDate >= from && t.QueueDate <= today)
            .Select(t => new { t.QueueDate, t.Status, t.CreatedAt, t.CalledAt })
            .ToListAsync(ct);

        return Enumerable.Range(0, take)
            .Select(i =>
            {
                var date = from.AddDays(i);
                var dayRows = rows.Where(r => r.QueueDate == date).ToList();
                var waited = dayRows.Where(r => r.CalledAt.HasValue).ToList();
                return new DailyTrendDto
                {
                    Date = date,
                    Total = dayRows.Count,
                    Completed = dayRows.Count(r => r.Status == QueueTicketStatus.Completed),
                    NoShow = dayRows.Count(r => r.Status == QueueTicketStatus.NoShow),
                    Cancelled = dayRows.Count(r => r.Status == QueueTicketStatus.Cancelled),
                    AverageWaitingMinutes = Round1(waited
                        .Select(r => (r.CalledAt!.Value - r.CreatedAt).TotalMinutes)
                        .DefaultIfEmpty(0)
                        .Average())
                };
            })
            .ToList();
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local;
        }
    }

    private static double Round1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
