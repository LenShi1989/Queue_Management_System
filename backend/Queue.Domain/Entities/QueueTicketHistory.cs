using Queue.Domain.Enums;

namespace Queue.Domain.Entities;

/// <summary>
/// 票據歷程（§21 Audit Log / §44.3 所有狀態異動都建立 History）
/// </summary>
public class QueueTicketHistory
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public QueueTicket? Ticket { get; set; }

    public QueueTicketStatus? FromStatus { get; set; }

    public QueueTicketStatus ToStatus { get; set; }

    public long? CounterId { get; set; }

    public string? OperatorId { get; set; }

    public string? OperatorName { get; set; }

    public QueueTicketAction Action { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? Remark { get; set; }
}
