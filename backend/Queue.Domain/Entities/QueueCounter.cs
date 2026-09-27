using Queue.Domain.Enums;

namespace Queue.Domain.Entities;

public class QueueCounter
{
    public long Id { get; set; }

    /// <summary>櫃台代碼，例如 1 / 2 / 3。</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>服務代碼；空值代表可處理所有服務類型。</summary>
    public string? ServiceCode { get; set; }

    public long? ServiceId { get; set; }

    public QueueService? Service { get; set; }

    public QueueCounterStatus Status { get; set; } = QueueCounterStatus.Idle;

    /// <summary>目前處理中的票據。</summary>
    public long? CurrentTicketId { get; set; }

    public QueueTicket? CurrentTicket { get; set; }

    /// <summary>權重：權重越高分派到越多票（§35 多櫃台策略）。</summary>
    public int Weight { get; set; } = 1;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
