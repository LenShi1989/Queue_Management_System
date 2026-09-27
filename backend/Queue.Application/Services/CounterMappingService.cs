using Microsoft.EntityFrameworkCore;
using Queue.Application.Abstractions;
using Queue.Application.Dtos;
using Queue.Domain.Common;
using Queue.Domain.Entities;
using Queue.Domain.Exceptions;

namespace Queue.Application.Services;

/// <summary>
/// 櫃台 → DTO 映射，供 CounterService 與 QueueCounterService 共用。
/// </summary>
public static class CounterMappingService
{
    public static async Task<CounterDto> MapAsync(IQueueDbContext db, QueueCounter counter, IClock clock, CancellationToken ct = default)
    {
        var service = counter.ServiceId.HasValue
            ? await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == counter.ServiceId.Value, ct)
            : null;

        var current = counter.CurrentTicketId.HasValue
            ? await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == counter.CurrentTicketId.Value, ct)
            : null;

        var today = clock.Today;
        var waiting = await db.Tickets.AsNoTracking()
            .CountAsync(t => t.QueueDate == today
                             && t.Status == Domain.Enums.QueueTicketStatus.Waiting
                             && (counter.ServiceId == null || t.ServiceId == counter.ServiceId), ct);

        return new CounterDto
        {
            Id = counter.Id,
            Code = counter.Code,
            Name = counter.Name,
            ServiceId = counter.ServiceId,
            ServiceName = service?.Name,
            ServiceCode = service?.Code,
            Status = counter.Status.ToString(),
            CurrentTicketId = counter.CurrentTicketId,
            CurrentTicketNo = current?.TicketNo,
            CurrentTicketStatus = current?.Status.ToString(),
            Weight = counter.Weight,
            DisplayOrder = counter.DisplayOrder,
            IsActive = counter.IsActive,
            WaitingCount = waiting
        };
    }
}
