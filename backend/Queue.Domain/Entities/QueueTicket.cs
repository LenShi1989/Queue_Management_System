using Queue.Domain.Enums;

namespace Queue.Domain.Entities;

public class QueueTicket
{
    public long Id { get; set; }

    public DateOnly QueueDate { get; set; }

    /// <summary>顯示號碼，例如 A001。</summary>
    public string TicketNo { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public int SequenceNo { get; set; }

    public long ServiceId { get; set; }

    public QueueService? Service { get; set; }

    public long? CounterId { get; set; }

    public QueueCounter? Counter { get; set; }

    public QueueTicketStatus Status { get; set; } = QueueTicketStatus.Waiting;

    /// <summary>有效優先權 = 服務基準 + 使用者自訂 + 老化加成（§17.2）。</summary>
    public int Priority { get; set; }

    /// <summary>排隊序位：同日期同服務遞增，用於快速計算前面人數。</summary>
    public long QueuePosition { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? QrTokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CalledAt { get; set; }

    public DateTimeOffset? ServingAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public DateTimeOffset? NoShowAt { get; set; }

    public int CallCount { get; set; }

    public string? Remark { get; set; }

    public ICollection<QueueTicketHistory> Histories { get; set; } = new List<QueueTicketHistory>();

    /// <summary>等待分鐘數（取號至被叫號）。</summary>
    public double WaitingMinutes => CalledAt.HasValue
        ? (CalledAt.Value - CreatedAt).TotalMinutes
        : (DateTimeOffset.UtcNow - CreatedAt).TotalMinutes;

    /// <summary>服務分鐘數（開始服務至完成）。</summary>
    public double? ServiceMinutes => ServingAt.HasValue && CompletedAt.HasValue
        ? (CompletedAt.Value - ServingAt.Value).TotalMinutes
        : null;

    /// <summary>總耗時分鐘數（取號至完成）。</summary>
    public double TotalMinutes => CompletedAt.HasValue
        ? (CompletedAt.Value - CreatedAt).TotalMinutes
        : (DateTimeOffset.UtcNow - CreatedAt).TotalMinutes;
}
