using Queue.Domain.Enums;

namespace Queue.Domain.Entities;

/// <summary>
/// 系統設定（§5.1 叫號設定 / 語音設定 / 顯示器設定 / 營業時間）
/// 以 Key-Value 儲存，支援覆寫資料庫預設。
/// </summary>
public class QueueSetting
{
    public long Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string? ValueType { get; set; } = "string";

    public string? Category { get; set; } = "General";

    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public static class QueueSettingKeys
{
    public const string BusinessHours = "queue.business_hours";
    public const string RecallTimeoutSeconds = "queue.recall_timeout_seconds";
    public const string MaxRecallCount = "queue.max_recall_count";
    public const string AverageServiceMinutes = "queue.average_service_minutes";
    public const string AgingFactor = "queue.aging_factor";
    public const string AgingEnabled = "queue.aging_enabled";
    public const string VoiceEnabled = "display.voice_enabled";
    public const string VoiceLanguage = "display.voice_language";
    public const string DisplayRotationSeconds = "display.rotation_seconds";
    public const string DisplayAds = "display.ads";
    public const string QrTokenLifetimeHours = "security.qr_token_lifetime_hours";
    public const string KioskAutoRefreshSeconds = "kiosk.auto_refresh_seconds";
}
