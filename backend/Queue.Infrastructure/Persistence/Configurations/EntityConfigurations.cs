using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Queue.Domain.Entities;
using Queue.Domain.Enums;

namespace Queue.Infrastructure.Persistence.Configurations;

public class QueueServiceConfiguration : IEntityTypeConfiguration<QueueService>
{
    public void Configure(EntityTypeBuilder<QueueService> builder)
    {
        builder.ToTable("queue_services");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Prefix).HasMaxLength(5).IsRequired();
        builder.Property(x => x.NumberLength).HasDefaultValue(3);
        builder.Property(x => x.Priority).HasDefaultValue(0);
        builder.Property(x => x.EstimatedServiceMinutes).HasDefaultValue(5);
        builder.Property(x => x.SkipLineEnabled).HasDefaultValue(false);
        builder.Property(x => x.DisplayOrder).HasDefaultValue(0);
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_queue_services_code");
        builder.HasIndex(x => x.Prefix).IsUnique().HasDatabaseName("ux_queue_services_prefix");
        builder.HasIndex(x => new { x.IsActive, x.DisplayOrder }).HasDatabaseName("ix_queue_services_active");
    }
}

public class QueueCounterConfiguration : IEntityTypeConfiguration<QueueCounter>
{
    public void Configure(EntityTypeBuilder<QueueCounter> builder)
    {
        builder.ToTable("queue_counters");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ServiceCode).HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<int>().HasDefaultValue(QueueCounterStatus.Idle);
        builder.Property(x => x.Weight).HasDefaultValue(1);
        builder.Property(x => x.DisplayOrder).HasDefaultValue(0);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_queue_counters_code");
        builder.HasIndex(x => new { x.IsActive, x.Status }).HasDatabaseName("ix_queue_counters_status");

        builder.HasOne(x => x.Service)
            .WithMany(s => s.Counters)
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.CurrentTicket)
            .WithMany()
            .HasForeignKey(x => x.CurrentTicketId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class QueueTicketConfiguration : IEntityTypeConfiguration<QueueTicket>
{
    public void Configure(EntityTypeBuilder<QueueTicket> builder)
    {
        builder.ToTable("queue_tickets");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.QueueDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.TicketNo).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Prefix).HasMaxLength(5).IsRequired();
        builder.Property(x => x.SequenceNo).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Priority).HasDefaultValue(0);
        builder.Property(x => x.CustomerName).HasMaxLength(50);
        builder.Property(x => x.CustomerPhone).HasMaxLength(20);
        builder.Property(x => x.QrTokenHash).HasMaxLength(64);
        builder.Property(x => x.Remark).HasMaxLength(200);
        builder.Property(x => x.CallCount).HasDefaultValue(0);

        // §14 欄位型別：UTC timestamp with time zone
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.CalledAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ServingAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CancelledAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.NoShowAt).HasColumnType("timestamp with time zone");

        // §16 PostgreSQL 建議索引
        builder.HasIndex(x => new { x.QueueDate, x.Status }).HasDatabaseName("ix_queue_ticket_date_status");
        builder.HasIndex(x => new { x.ServiceId, x.Status }).HasDatabaseName("ix_queue_ticket_service_status");
        builder.HasIndex(x => new { x.CounterId, x.Status }).HasDatabaseName("ix_queue_ticket_counter_status");
        builder.HasIndex(x => new { x.QueueDate, x.Prefix, x.SequenceNo })
            .IsUnique()
            .HasDatabaseName("ux_queue_ticket_date_prefix_sequence");

        builder.HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Histories)
            .WithOne(h => h.Ticket!)
            .HasForeignKey(h => h.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class QueueTicketHistoryConfiguration : IEntityTypeConfiguration<QueueTicketHistory>
{
    public void Configure(EntityTypeBuilder<QueueTicketHistory> builder)
    {
        builder.ToTable("queue_ticket_histories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.FromStatus).HasConversion<int?>();
        builder.Property(x => x.ToStatus).HasConversion<int>().IsRequired();
        builder.Property(x => x.Action).HasConversion<int>().IsRequired();
        builder.Property(x => x.OperatorId).HasMaxLength(50);
        builder.Property(x => x.OperatorName).HasMaxLength(50);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(400);
        builder.Property(x => x.Remark).HasMaxLength(400);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(x => new { x.TicketId, x.Id }).HasDatabaseName("ix_queue_history_ticket");
        builder.HasIndex(x => new { x.Action, x.CreatedAt }).HasDatabaseName("ix_queue_history_action");
    }
}

public class QueueDailySequenceConfiguration : IEntityTypeConfiguration<QueueDailySequence>
{
    public void Configure(EntityTypeBuilder<QueueDailySequence> builder)
    {
        builder.ToTable("queue_daily_sequences");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.QueueDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.Prefix).HasMaxLength(5).IsRequired();
        builder.Property(x => x.CurrentNumber).HasDefaultValue(0);
        builder.Property(x => x.NumberLength).HasDefaultValue(3);
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired();

        // 併發取號的關鍵：唯一約束讓 UPSERT / ON CONFLICT 可運作
        builder.HasIndex(x => new { x.QueueDate, x.ServiceId })
            .IsUnique()
            .HasDatabaseName("ux_queue_daily_sequence_date_service");
    }
}

public class QueueSettingConfiguration : IEntityTypeConfiguration<QueueSetting>
{
    public void Configure(EntityTypeBuilder<QueueSetting> builder)
    {
        builder.ToTable("queue_settings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(2000);
        builder.Property(x => x.ValueType).HasMaxLength(20);
        builder.Property(x => x.Category).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.Property(x => x.IsSystem).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("ux_queue_settings_key");
    }
}

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_users");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.UserName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(100);
        builder.Property(x => x.LastLoginIp).HasMaxLength(64);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.LastLoginAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LockoutEndAt).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.UserName).IsUnique().HasDatabaseName("ux_app_users_username");
        builder.HasIndex(x => x.CounterId).HasDatabaseName("ix_app_users_counter");

        builder.HasOne(x => x.Counter)
            .WithMany()
            .HasForeignKey(x => x.CounterId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.UserRoles)
            .WithOne(ur => ur.User!)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        builder.ToTable("app_roles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200);

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_app_roles_code");
    }
}

public class AppUserRoleConfiguration : IEntityTypeConfiguration<AppUserRole>
{
    public void Configure(EntityTypeBuilder<AppUserRole> builder)
    {
        builder.ToTable("app_user_roles");
        builder.HasKey(x => new { x.UserId, x.RoleId });

        builder.HasOne(x => x.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
