namespace Queue.Domain.Entities;

/// <summary>
/// 每日號碼序列（§15.5 / §7.4 併發安全）
/// 以 (QueueDate, ServiceId) 唯一，透過 Row Lock + Transaction 取號。
/// </summary>
public class QueueDailySequence
{
    public long Id { get; set; }

    public DateOnly QueueDate { get; set; }

    public long ServiceId { get; set; }

    public string Prefix { get; set; } = string.Empty;

    public int CurrentNumber { get; set; }

    public int NumberLength { get; set; } = 3;

    public DateTimeOffset UpdatedAt { get; set; }
}
