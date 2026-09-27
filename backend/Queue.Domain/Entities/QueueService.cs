using Queue.Domain.Enums;

namespace Queue.Domain.Entities;

public class QueueService
{
    public long Id { get; set; }

    /// <summary>服務代碼，例如 GENERAL / VIP。</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>號碼前綴，例如 A / V / R。</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>號碼位數，例如 3 → A001。</summary>
    public int NumberLength { get; set; } = 3;

    /// <summary>基準優先權，越大越優先（§17.1 ORDER BY priority DESC）。</summary>
    public int Priority { get; set; }

    /// <summary>預估服務分鐘數，用於預估等待時間（§18）。</summary>
    public int EstimatedServiceMinutes { get; set; } = 5;

    /// <summary>是否啟用優先權加成（VIP 插隊）。</summary>
    public bool SkipLineEnabled { get; set; }

    public int DisplayOrder { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<QueueCounter> Counters { get; set; } = new List<QueueCounter>();
}
