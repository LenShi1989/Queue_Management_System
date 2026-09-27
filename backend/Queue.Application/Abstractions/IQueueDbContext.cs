using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Queue.Domain.Common;

namespace Queue.Application.Abstractions;

/// <summary>
/// 排隊交易範圍：Application 服務用此取得 DbSet，並以 Row Lock 確保號碼併發安全。
/// 實作於 Infrastructure（QueueDbContext）。
/// </summary>
public interface IQueueDbContext : IUnitOfWork
{
    DbSet<Domain.Entities.QueueTicket> Tickets { get; }

    DbSet<Domain.Entities.QueueService> Services { get; }

    DbSet<Domain.Entities.QueueCounter> Counters { get; }

    DbSet<Domain.Entities.QueueTicketHistory> Histories { get; }

    DbSet<Domain.Entities.QueueDailySequence> DailySequences { get; }

    DbSet<Domain.Entities.QueueSetting> Settings { get; }

    DbSet<Domain.Entities.AppUser> Users { get; }

    DbSet<Domain.Entities.AppRole> Roles { get; }

    DbSet<Domain.Entities.AppUserRole> UserRoles { get; }

    DatabaseFacade Database { get; }
}

/// <summary>
/// 即時推播（SignalR）。實作於 API 層的 HubContext 封裝，避免 Application 依賴 SignalR。
/// </summary>
public interface IQueueNotifier
{
    Task TicketCreatedAsync(TicketEventNotification notification, CancellationToken ct = default);

    Task TicketCalledAsync(TicketEventNotification notification, CancellationToken ct = default);

    Task TicketRecalledAsync(TicketEventNotification notification, CancellationToken ct = default);

    Task TicketStartedAsync(TicketEventNotification notification, CancellationToken ct = default);

    Task TicketCompletedAsync(TicketEventNotification notification, CancellationToken ct = default);

    Task TicketNoShowAsync(TicketEventNotification notification, CancellationToken ct = default);

    Task TicketCancelledAsync(TicketEventNotification notification, CancellationToken ct = default);

    Task TicketTransferredAsync(TicketEventNotification notification, CancellationToken ct = default);

    Task TicketUpdatedAsync(TicketEventNotification notification, CancellationToken ct = default);

    Task DisplayUpdatedAsync(DisplaySnapshot snapshot, CancellationToken ct = default);
}

/// <summary>
/// 票據即時事件內容（§13 Event 範例）
/// </summary>
public class TicketEventNotification
{
    public long TicketId { get; init; }

    public string TicketNo { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Prefix { get; init; }

    public long? ServiceId { get; init; }

    public string? ServiceName { get; init; }

    /// <summary>服務代碼（用於 SignalR 的 service:{code} 群組訂閱）。</summary>
    public string? ServiceCode { get; init; }

    public long? CounterId { get; init; }

    public string? CounterNo { get; init; }

    public int? Position { get; init; }

    public int? EstimatedMinutes { get; init; }

    public int Priority { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public string? Message { get; init; }
}

/// <summary>
/// 叫號顯示器快照（§10.4 / §11.3 GET /api/queue/display）
/// </summary>
public class DisplaySnapshot
{
    public DateTimeOffset Timestamp { get; init; }

    public int WaitingTotal { get; init; }

    public int? WaitingTotalWithPriority { get; init; }

    public IReadOnlyList<DisplayCounterState> Counters { get; init; } = [];

    public IReadOnlyList<DisplayServiceState> Services { get; init; } = [];

    public IReadOnlyList<DisplayTicket> RecentCalls { get; init; } = [];
}

public class DisplayCounterState
{
    public long CounterId { get; init; }

    public string CounterNo { get; init; } = string.Empty;

    public string CounterName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DisplayTicket? Current { get; init; }

    public IReadOnlyList<DisplayTicket> Waiting { get; init; } = [];
}

public class DisplayServiceState
{
    public long ServiceId { get; init; }

    public string ServiceName { get; init; } = string.Empty;

    public string Prefix { get; init; } = string.Empty;

    public int WaitingCount { get; init; }

    public int ServingCount { get; init; }

    public string? CurrentTicketNo { get; init; }
}

public class DisplayTicket
{
    public long TicketId { get; init; }

    public string TicketNo { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset? CalledAt { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}
