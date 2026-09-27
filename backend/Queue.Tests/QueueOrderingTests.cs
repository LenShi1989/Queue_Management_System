using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Queue.Application.Common;
using Queue.Application.Dtos;
using Queue.Application.Services;
using Queue.Domain.Common;
using Queue.Domain.Entities;
using Queue.Domain.Enums;
using Queue.Infrastructure.Persistence;

namespace Queue.Tests;

/// <summary>
/// 驗證 §17.1 排隊排序規則（priority DESC, created_at ASC, id ASC）
/// 與 §18 等待時間估算。此為整個叫號流程的核心排序語意。
/// </summary>
public class QueueOrderingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static (QueueDbContext Db, WaitTimeEstimator Estimator) CreateSut()
    {
        var options = new DbContextOptionsBuilder<QueueDbContext>()
            .UseInMemoryDatabase($"queue-ordering-{Guid.NewGuid()}")
            .Options;

        var db = new QueueDbContext(options);

        var service = new QueueService
        {
            Id = 1,
            Code = "GENERAL",
            Name = "一般服務",
            Prefix = "A",
            NumberLength = 3,
            Priority = 0,
            EstimatedServiceMinutes = 5,
            IsActive = true,
            CreatedAt = T0,
            UpdatedAt = T0
        };
        db.Services.Add(service);

        var settings = new SettingProvider(db, Options.Create(new QueueOptions()), NullLogger<SettingProvider>.Instance);
        var estimator = new WaitTimeEstimator(db, settings, new FixedClock(T0.AddMinutes(30)), NullLogger<WaitTimeEstimator>.Instance);

        return (db, estimator);
    }

    private static QueueTicket NewTicket(long id, string no, int priority, int minutesAfterStart)
        => new()
        {
            Id = id,
            QueueDate = DateOnly.FromDateTime(T0.DateTime),
            TicketNo = no,
            Prefix = "A",
            SequenceNo = (int)id,
            ServiceId = 1,
            Status = QueueTicketStatus.Waiting,
            Priority = priority,
            CreatedAt = T0.AddMinutes(minutesAfterStart)
        };

    [Fact]
    public async Task 同優先權_應依建立時間由早到晚()
    {
        var (db, estimator) = CreateSut();
        var a = NewTicket(1, "A001", 0, 0);
        var b = NewTicket(2, "A002", 0, 5);
        var c = NewTicket(3, "A003", 0, 10);
        db.Tickets.AddRange(a, b, c);
        await db.SaveChangesAsync();

        Assert.Equal(0, (await estimator.CountAheadAsync(a, default)).PeopleAhead);
        Assert.Equal(1, (await estimator.CountAheadAsync(b, default)).PeopleAhead);
        Assert.Equal(2, (await estimator.CountAheadAsync(c, default)).PeopleAhead);
    }

    [Fact]
    public async Task 高優先權_應排在低優先權之前()
    {
        var (db, estimator) = CreateSut();
        var normal = NewTicket(1, "A001", 0, 0);      // 先建立但權限低
        var vip = NewTicket(2, "V001", 100, 10);     // 後建立但權限高
        db.Tickets.AddRange(normal, vip);
        await db.SaveChangesAsync();

        // VIP 權限高，即使較晚建立仍排在一般前面
        Assert.Equal(0, (await estimator.CountAheadAsync(vip, default)).PeopleAhead);
        Assert.Equal(1, (await estimator.CountAheadAsync(normal, default)).PeopleAhead);
    }

    [Fact]
    public async Task 同分同時間_應以Id較小者優先()
    {
        var (db, estimator) = CreateSut();
        var first = NewTicket(1, "A001", 0, 0);
        var second = NewTicket(2, "A002", 0, 0); // CreatedAt 完全相同
        db.Tickets.AddRange(first, second);
        await db.SaveChangesAsync();

        Assert.Equal(0, (await estimator.CountAheadAsync(first, default)).PeopleAhead);
        Assert.Equal(1, (await estimator.CountAheadAsync(second, default)).PeopleAhead);
    }

    [Fact]
    public async Task 非Waiting狀態_不應計入前方人數()
    {
        var (db, estimator) = CreateSut();
        var target = NewTicket(5, "A005", 0, 0);

        // 這些都排在 target 前面，但已離開 Waiting
        db.Tickets.AddRange(
            NewTicket(1, "A001", 0, 0),
            NewTicket(2, "A002", 0, 0),
            NewTicket(3, "A003", 0, 0));
        db.Tickets.Add(target);

        foreach (var id in new long[] { 1, 2, 3 })
        {
            var t = await db.Tickets.FindAsync(id);
            t!.Status = id switch
            {
                1 => QueueTicketStatus.Calling,
                2 => QueueTicketStatus.Serving,
                _ => QueueTicketStatus.Cancelled
            };
        }
        await db.SaveChangesAsync();

        Assert.Equal(0, (await estimator.CountAheadAsync(target, default)).PeopleAhead);
    }

    [Fact]
    public async Task 不同服務類型_不應互相影響()
    {
        var (db, estimator) = CreateSut();
        var other = new QueueService
        {
            Id = 2, Code = "VIP", Name = "VIP", Prefix = "V", NumberLength = 3,
            IsActive = true, CreatedAt = T0, UpdatedAt = T0
        };
        db.Services.Add(other);

        var mine = NewTicket(10, "A001", 0, 0);
        var theirs = new QueueTicket
        {
            Id = 11, QueueDate = DateOnly.FromDateTime(T0.DateTime), TicketNo = "V001", Prefix = "V",
            ServiceId = 2, Status = QueueTicketStatus.Waiting, CreatedAt = T0.AddMinutes(-100)
        };
        db.Tickets.AddRange(mine, theirs);
        await db.SaveChangesAsync();

        Assert.Equal(0, (await estimator.CountAheadAsync(mine, default)).PeopleAhead);
    }

    [Fact]
    public async Task Position_應等於前方人數加一()
    {
        var (db, estimator) = CreateSut();
        db.Tickets.AddRange(
            NewTicket(1, "A001", 0, 0),
            NewTicket(2, "A002", 0, 5),
            NewTicket(3, "A003", 0, 10));
        await db.SaveChangesAsync();

        var third = await db.Tickets.FindAsync(3L);
        var (ahead, position) = await estimator.CountAheadAsync(third!, default);

        Assert.Equal(2, ahead);
        Assert.Equal(3, position);
    }

    [Fact]
    public async Task EstimateMinutes_應為前方人數乘以平均服務分鐘()
    {
        var (db, estimator) = CreateSut();
        db.Tickets.AddRange(
            NewTicket(1, "A001", 0, 0),
            NewTicket(2, "A002", 0, 5),
            NewTicket(3, "A003", 0, 10));
        await db.SaveChangesAsync();

        var third = await db.Tickets.FindAsync(3L);
        var minutes = await estimator.EstimateMinutesAsync(third!, 2, default);

        // 平均服務 5 分 × 2 人 = 10 分
        Assert.Equal(10, minutes);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
        public DateTimeOffset Now => now.ToLocalTime();
        public DateOnly Today => DateOnly.FromDateTime(now.DateTime);
    }
}
