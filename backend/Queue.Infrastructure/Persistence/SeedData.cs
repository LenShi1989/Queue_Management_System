using Microsoft.EntityFrameworkCore;
using Queue.Domain.Entities;
using Queue.Domain.Enums;

namespace Queue.Infrastructure.Persistence;

/// <summary>
/// 初始資料（Roles、服務類型、櫃台、系統設定、管理帳號）
/// 以 HasData 寫入 Migration，確保任何環境都能取得一致的基础設定。
/// </summary>
public static class SeedData
{
    /// <summary>測試/預設管理員帳號：admin / a12345678（部署後務必修改）。</summary>
    public const string DefaultAdminUserName = "admin";

    public const string DefaultAdminPassword = "a12345678";

    /// <summary>
    /// 固定 salt 的 PBKDF2 結果，確保 Migration 可重複產生且不會因重新雜湊而產生無意義差異。
    /// 格式：iterations.salt.hash（PBKDF2-SHA256, 210000 iterations）
    /// </summary>
    private const string AdminPasswordHash = "210000.prHx6RnMQ/c9Qi6+80IedA==.rCnw2+H1R2dVAL3XUvPXzKBYXhNyo/xsh+aBXWmTBOw=";

    private const string CounterPasswordHash = "210000.Fe1nLmiqK0P0MqQma/BPeA==.+HLJhyKdsN5whTnM4GSoetT0ZcvWHJJBn3MNusEX6IA=";

    private const string KioskPasswordHash = "210000.asZbnO3AHduCrzY6mLzToA==.5CGPtI6RSYR1tHob0Sdvzr0hwsU9AFQW/BwPpxn22os=";

    public static readonly DateTimeOffset SeedTime = DateTimeOffset.UnixEpoch;

    public static void Apply(ModelBuilder modelBuilder)
    {
        SeedRoles(modelBuilder);
        SeedServices(modelBuilder);
        SeedCounters(modelBuilder);
        SeedSettings(modelBuilder);
        SeedUsers(modelBuilder);
    }

    private static void SeedRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppRole>().HasData(
            Role(1, QueueRoles.Admin, "系統管理員", "使用者、角色、服務、櫃台、佇列、系統設定、統計"),
            Role(2, QueueRoles.Manager, "管理者", "佇列、櫃台、統計、部分設定"),
            Role(3, QueueRoles.Counter, "櫃台服務人員", "叫號、再叫、開始、完成、過號、轉移"),
            Role(4, QueueRoles.Display, "叫號顯示器", "讀取佇列狀態、接收 SignalR 事件"),
            Role(5, QueueRoles.Kiosk, "自助取號設備", "取號、讀取服務"),
            Role(6, QueueRoles.Mobile, "手機查詢使用者", "以 QR Token 查詢自己的票據進度"));

        static AppRole Role(long id, string code, string name, string description) => new()
        {
            Id = id,
            Code = code,
            Name = name,
            Description = description
        };
    }

    private static void SeedServices(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QueueService>().HasData(
            new QueueService
            {
                Id = 1,
                Code = "GENERAL",
                Name = "一般服務",
                Prefix = "A",
                NumberLength = 3,
                Priority = 0,
                EstimatedServiceMinutes = 5,
                SkipLineEnabled = false,
                DisplayOrder = 1,
                Description = "一般業務辦理",
                IsActive = true,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime
            },
            new QueueService
            {
                Id = 2,
                Code = "VIP",
                Name = "VIP 服務",
                Prefix = "V",
                NumberLength = 3,
                Priority = 100,
                EstimatedServiceMinutes = 10,
                SkipLineEnabled = true,
                DisplayOrder = 2,
                Description = "VIP 貴賓專屬服務，優先叫號",
                IsActive = true,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime
            },
            new QueueService
            {
                Id = 3,
                Code = "APPOINTMENT",
                Name = "預約報到",
                Prefix = "R",
                NumberLength = 3,
                Priority = 50,
                EstimatedServiceMinutes = 8,
                SkipLineEnabled = false,
                DisplayOrder = 3,
                Description = "已預約客戶報到專用",
                IsActive = true,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime
            },
            new QueueService
            {
                Id = 4,
                Code = "EXPRESS",
                Name = "急件處理",
                Prefix = "E",
                NumberLength = 3,
                Priority = 200,
                EstimatedServiceMinutes = 3,
                SkipLineEnabled = true,
                DisplayOrder = 4,
                Description = "急件/插單服務",
                IsActive = true,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime
            });
    }

    private static void SeedCounters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QueueCounter>().HasData(
            Counter(1, "1", "1 號櫃台", 1, 1, 1),
            Counter(2, "2", "2 號櫃台", 2, 1, 2),
            Counter(3, "3", "3 號櫃台", 3, 1, 3),
            Counter(4, "4", "4 號櫃台（VIP）", 2, 1, 4),
            Counter(5, "5", "5 號櫃台（預約）", 3, 1, 5));

        static QueueCounter Counter(long id, string code, string name, long serviceId, int weight, int order) => new()
        {
            Id = id,
            Code = code,
            Name = name,
            ServiceId = serviceId,
            ServiceCode = serviceId switch
            {
                1 => "GENERAL",
                2 => "VIP",
                3 => "APPOINTMENT",
                4 => "EXPRESS",
                _ => null
            },
            Status = QueueCounterStatus.Idle,
            Weight = weight,
            DisplayOrder = order,
            IsActive = true,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime
        };
    }

    private static void SeedSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QueueSetting>().HasData(
            Setting(1, "queue.business_hours", "08:00-17:00", "string", "Queue", "營業時間（每日重新編號起訖）", true),
            Setting(2, "queue.recall_timeout_seconds", "180", "int", "Queue", "叫號後等待逾時秒數", true),
            Setting(3, "queue.max_recall_count", "2", "int", "Queue", "同一票據最大再叫次數，超過自動過號", true),
            Setting(4, "queue.average_service_minutes", "5", "int", "Queue", "全站平均服務分鐘數（預估等待時間）", true),
            Setting(5, "queue.aging_enabled", "false", "bool", "Queue", "是否啟用等待老化優先權加成（§17.2）", true),
            Setting(6, "queue.aging_factor", "10", "int", "Queue", "等待老化因子：每 N 分鐘 +1 優先權", true),
            Setting(7, "display.voice_enabled", "true", "bool", "Display", "叫號語音播放", true),
            Setting(8, "display.voice_language", "zh-TW", "string", "Display", "語音語言（Web Speech API）", true),
            Setting(9, "display.rotation_seconds", "10", "int", "Display", "廣告輪播間隔秒數", true),
            Setting(10, "display.ads", "歡迎光臨服務大廳｜請留意叫號資訊", "string", "Display", "顯示器廣告輪播內容（以 ｜ 分隔）", true),
            Setting(11, "security.qr_token_lifetime_hours", "72", "int", "Security", "QR Code Token 有效小時數", true),
            Setting(12, "kiosk.auto_refresh_seconds", "10", "int", "Kiosk", "Kiosk 取號畫面自動重置秒數", true));
    }

    private static void SeedUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasData(
            new AppUser
            {
                Id = 1,
                UserName = DefaultAdminUserName,
                PasswordHash = AdminPasswordHash,
                DisplayName = "系統管理員",
                Email = "admin@queue.local",
                IsActive = true,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime
            },
            new AppUser
            {
                Id = 2,
                UserName = "counter1",
                PasswordHash = CounterPasswordHash,
                DisplayName = "櫃台服務人員 1",
                CounterId = 1,
                IsActive = true,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime
            },
            new AppUser
            {
                Id = 3,
                UserName = "kiosk",
                PasswordHash = KioskPasswordHash,
                DisplayName = "Kiosk 服務",
                IsActive = true,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime
            });

        modelBuilder.Entity<AppUserRole>().HasData(
            new AppUserRole { UserId = 1, RoleId = 1 },
            new AppUserRole { UserId = 1, RoleId = 2 },
            new AppUserRole { UserId = 2, RoleId = 3 },
            new AppUserRole { UserId = 3, RoleId = 5 });
    }

    private static QueueSetting Setting(
        long id,
        string key,
        string value,
        string valueType,
        string category,
        string description,
        bool isSystem) => new()
    {
        Id = id,
        Key = key,
        Value = value,
        ValueType = valueType,
        Category = category,
        Description = description,
        IsSystem = isSystem,
        CreatedAt = SeedTime,
        UpdatedAt = SeedTime
    };
}
