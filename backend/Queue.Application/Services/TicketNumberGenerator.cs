using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Queue.Application.Abstractions;
using Queue.Application.Common;
using Queue.Domain.Common;
using Queue.Domain.Entities;
using Queue.Domain.Enums;
using Queue.Domain.Exceptions;

namespace Queue.Application.Services;

/// <summary>
/// 號碼產生器（§7.4 併發安全）
/// 禁止 MAX(SequenceNo)+1；以 QueueDailySequence 表 + Row Lock (FOR UPDATE) + Transaction 實作。
/// 第一次取號時以 UPSERT 建立當日序列列，後續請求以 row lock 序列化遞增。
/// </summary>
public class TicketNumberGenerator
{
    private readonly IQueueDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<TicketNumberGenerator> _logger;

    public TicketNumberGenerator(IQueueDbContext db, IClock clock, ILogger<TicketNumberGenerator> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// 取得下一組號碼。必須在交易中呼叫；會對 (QueueDate, ServiceId) 對應的序列列取 row lock。
    /// </summary>
    public async Task<(int SequenceNo, string TicketNo)> NextAsync(
        QueueService service,
        DateOnly queueDate,
        CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        // 1. 嘗試以 UPSERT 建立序列列（併發時可能多列同時搶佔，但 UNIQUE 約束只會有一列成功）
        var created = await TryEnsureSequenceRowAsync(service, queueDate, now, ct);

        if (!created)
        {
            // 2. 序列列已存在 → FOR UPDATE 鎖住，序列化本交易內的取號
            var locked = await _db.DailySequences
                .FromSqlInterpolated($"""
                    SELECT * FROM queue_daily_sequences
                    WHERE queue_date = {queueDate} AND service_id = {service.Id}
                    FOR UPDATE
                    """)
                .AsTracking()
                .SingleAsync(ct);

            var nextNumber = locked.CurrentNumber + 1;
            locked.CurrentNumber = nextNumber;
            locked.Prefix = service.Prefix;
            locked.NumberLength = service.NumberLength;
            locked.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);

            return (nextNumber, Format(service.Prefix, nextNumber, service.NumberLength));
        }

        // 3. 新建的序列列目前由本交易持有（INSERT 取得隱含 exclusive lock）
        var row = await _db.DailySequences
            .SingleAsync(x => x.QueueDate == queueDate && x.ServiceId == service.Id, ct);

        row.CurrentNumber = 1;
        row.Prefix = service.Prefix;
        row.NumberLength = service.NumberLength;
        row.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        return (1, Format(service.Prefix, 1, service.NumberLength));
    }

    private async Task<bool> TryEnsureSequenceRowAsync(QueueService service, DateOnly queueDate, DateTimeOffset now, CancellationToken ct)
    {
        // PostgreSQL: INSERT ... ON CONFLICT DO NOTHING RETURNING id → 有回傳代表本次建立成功
        // 注意：EF 的 SqlQuery<long> 會包成 SELECT s.Value FROM (...) s，故欄位需別名為 "Value"
        var inserted = await _db.Database.SqlQuery<long>($"""
            INSERT INTO queue_daily_sequences
                (queue_date, service_id, prefix, current_number, number_length, updated_at)
            VALUES ({queueDate}, {service.Id}, {service.Prefix}, 0, {service.NumberLength}, {now})
            ON CONFLICT (queue_date, service_id) DO NOTHING
            RETURNING id AS "Value"
            """).ToListAsync(ct);

        if (inserted.Count == 0)
        {
            return false;
        }

        _logger.LogDebug("建立每日序列列 Date={QueueDate} ServiceId={ServiceId}", queueDate, service.Id);
        return true;
    }

    public static string Format(string prefix, int sequenceNo, int numberLength)
    {
        var p = string.IsNullOrWhiteSpace(prefix) ? "A" : prefix.Trim().ToUpperInvariant();
        var len = numberLength is < 1 or > 10 ? 3 : numberLength;
        return $"{p}{sequenceNo.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(len, '0')}";
    }
}

/// <summary>
/// 設定讀取快取（§5.1 各種設定）
/// </summary>
public class SettingProvider
{
    private readonly IQueueDbContext _db;
    private readonly QueueOptions _queueOptions;
    private readonly ILogger<SettingProvider> _logger;

    public SettingProvider(IQueueDbContext db, IOptions<QueueOptions> queueOptions, ILogger<SettingProvider> logger)
    {
        _db = db;
        _queueOptions = queueOptions.Value;
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string key, T fallback, CancellationToken ct = default)
    {
        var setting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting?.Value is null)
        {
            return fallback;
        }

        try
        {
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (targetType == typeof(bool))
            {
                return (T)(object)bool.Parse(setting.Value);
            }

            if (targetType.IsEnum)
            {
                return (T)Enum.Parse(targetType, setting.Value, ignoreCase: true);
            }

            return (T)Convert.ChangeType(setting.Value, targetType, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or ArgumentException)
        {
            _logger.LogWarning(ex, "設定 {Key} 值 {Value} 無法轉換為 {Type}，使用預設值", key, setting.Value, typeof(T).Name);
            return fallback;
        }
    }

    public double DefaultAverageServiceMinutes => _queueOptions.DefaultAverageServiceMinutes;

    public bool AgingEnabled => _queueOptions.AgingEnabled;

    public int AgingFactor => _queueOptions.AgingFactorMinutes;

    public int MaxRecallCount => _queueOptions.MaxRecallCount;
}
