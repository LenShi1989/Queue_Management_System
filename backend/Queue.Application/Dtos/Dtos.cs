using System.ComponentModel.DataAnnotations;

namespace Queue.Application.Dtos;

// ─────────────────────────── Auth ───────────────────────────

public class LoginRequest
{
    [Required(ErrorMessage = "請輸入帳號")]
    [StringLength(50, MinimumLength = 2)]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入密碼")]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    public long? CounterId { get; set; }
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string TokenType { get; set; } = "Bearer";

    public int ExpiresIn { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public long? CounterId { get; set; }

    public string? CounterNo { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = [];
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; } = string.Empty;
}

public class UserDto
{
    public long Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public long? CounterId { get; set; }

    public string? CounterNo { get; set; }

    public bool IsActive { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = [];

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateUserRequest
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    public long? CounterId { get; set; }

    [MinLength(1)]
    public List<string> Roles { get; set; } = [];
}

public class UpdateUserRequest
{
    [StringLength(50)]
    public string? DisplayName { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public long? CounterId { get; set; }

    public bool? IsActive { get; set; }

    public List<string>? Roles { get; set; }
}

// ─────────────────────────── Service ───────────────────────────

public class ServiceDto
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public int NumberLength { get; set; }

    public int Priority { get; set; }

    public int EstimatedServiceMinutes { get; set; }

    public bool SkipLineEnabled { get; set; }

    public int DisplayOrder { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public int? WaitingCount { get; set; }

    public int? ServingCount { get; set; }

    public string? CurrentTicketNo { get; set; }
}

public class UpsertServiceRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(5, MinimumLength = 1)]
    public string Prefix { get; set; } = "A";

    [Range(1, 10)]
    public int NumberLength { get; set; } = 3;

    [Range(0, 1000)]
    public int Priority { get; set; }

    [Range(1, 600)]
    public int EstimatedServiceMinutes { get; set; } = 5;

    public bool SkipLineEnabled { get; set; }

    [Range(0, 999)]
    public int DisplayOrder { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

// ─────────────────────────── Counter ───────────────────────────

public class CounterDto
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public long? ServiceId { get; set; }

    public string? ServiceName { get; set; }

    public string? ServiceCode { get; set; }

    public string Status { get; set; } = string.Empty;

    public long? CurrentTicketId { get; set; }

    public string? CurrentTicketNo { get; set; }

    public string? CurrentTicketStatus { get; set; }

    public int Weight { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public int? WaitingCount { get; set; }
}

public class UpsertCounterRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public long? ServiceId { get; set; }

    [Range(1, 100)]
    public int Weight { get; set; } = 1;

    [Range(0, 999)]
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public class TransferRequest
{
    [Required]
    public long TargetCounterId { get; set; }

    [StringLength(200)]
    public string? Remark { get; set; }
}

// ─────────────────────────── Ticket ───────────────────────────

public class CreateTicketRequest
{
    [Required]
    [Range(1, long.MaxValue)]
    public long ServiceId { get; set; }

    [Range(0, 1000)]
    public int Priority { get; set; }

    [StringLength(50)]
    public string? CustomerName { get; set; }

    [StringLength(20)]
    public string? CustomerPhone { get; set; }

    [StringLength(200)]
    public string? Remark { get; set; }
}

public class TicketDto
{
    public long TicketId { get; set; }

    public long Id { get; set; }

    public string TicketNo { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateOnly QueueDate { get; set; }

    public string Prefix { get; set; } = string.Empty;

    public int SequenceNo { get; set; }

    public long ServiceId { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public long? CounterId { get; set; }

    public string? CounterNo { get; set; }

    public int Priority { get; set; }

    public int? Position { get; set; }

    public int? PeopleAhead { get; set; }

    public int? EstimatedMinutes { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? QrToken { get; set; }

    public string? QrUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CalledAt { get; set; }

    public DateTimeOffset? ServingAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public double? WaitingMinutes { get; set; }

    public double? ServiceMinutes { get; set; }

    public int CallCount { get; set; }

    public string? Remark { get; set; }
}

public class CallNextResultDto
{
    public long TicketId { get; set; }

    public string TicketNo { get; set; } = string.Empty;

    public long CounterId { get; set; }

    public string CounterNo { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int? Position { get; set; }

    public int? PeopleAhead { get; set; }

    public int? EstimatedMinutes { get; set; }

    public long? ServiceId { get; set; }

    public string? ServiceName { get; set; }
}

public class TicketStatusDto
{
    public long TicketId { get; set; }

    public string TicketNo { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? CurrentCallingNo { get; set; }

    public string? CounterNo { get; set; }

    public int? Position { get; set; }

    public int? PeopleAhead { get; set; }

    public int? EstimatedMinutes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CalledAt { get; set; }

    public DateTimeOffset? ServingAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public class TicketHistoryDto
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public string TicketNo { get; set; } = string.Empty;

    public string? FromStatus { get; set; }

    public string ToStatus { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public long? CounterId { get; set; }

    public string? CounterNo { get; set; }

    public string? OperatorName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? IpAddress { get; set; }

    public string? Remark { get; set; }
}

public class TicketQuery
{
    public DateOnly? Date { get; set; }

    public long? ServiceId { get; set; }

    public long? CounterId { get; set; }

    public string? Status { get; set; }

    public string? TicketNo { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

// ─────────────────────────── Statistics ───────────────────────────

public class TodayStatisticsDto
{
    public DateOnly Date { get; set; }

    public int TotalTickets { get; set; }

    public int CompletedCount { get; set; }

    public int WaitingCount { get; set; }

    public int CallingCount { get; set; }

    public int ServingCount { get; set; }

    public int NoShowCount { get; set; }

    public int CancelledCount { get; set; }

    public int TransferredCount { get; set; }

    public double AverageWaitingMinutes { get; set; }

    public double AverageServiceMinutes { get; set; }

    public double AverageTotalMinutes { get; set; }

    public double NoShowRate { get; set; }

    public double CancelRate { get; set; }

    public int ActiveCounters { get; set; }

    public int IdleCounters { get; set; }

    public IReadOnlyList<ServiceStatisticDto> Services { get; set; } = [];

    public IReadOnlyList<CounterStatisticDto> Counters { get; set; } = [];

    public IReadOnlyList<HourlyStatisticDto> Hourly { get; set; } = [];
}

public class ServiceStatisticDto
{
    public long ServiceId { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public int Total { get; set; }

    public int Completed { get; set; }

    public int Waiting { get; set; }

    public int NoShow { get; set; }

    public int Cancelled { get; set; }

    public double AverageServiceMinutes { get; set; }
}

public class CounterStatisticDto
{
    public long CounterId { get; set; }

    public string CounterNo { get; set; } = string.Empty;

    public string CounterName { get; set; } = string.Empty;

    public int CalledCount { get; set; }

    public int CompletedCount { get; set; }

    public int NoShowCount { get; set; }

    public double AverageServiceMinutes { get; set; }
}

public class HourlyStatisticDto
{
    public int Hour { get; set; }

    public int Total { get; set; }

    public int Completed { get; set; }

    public int NoShow { get; set; }

    public int Cancelled { get; set; }
}

public class DailyTrendDto
{
    public DateOnly Date { get; set; }

    public int Total { get; set; }

    public int Completed { get; set; }

    public int NoShow { get; set; }

    public int Cancelled { get; set; }

    public double AverageWaitingMinutes { get; set; }
}

public class PeakHourDto
{
    public int Hour { get; set; }

    public int Total { get; set; }

    public double Ratio { get; set; }
}

// ─────────────────────────── Setting ───────────────────────────

public class SettingDto
{
    public long Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string? ValueType { get; set; }

    public string? Category { get; set; }

    public string? Description { get; set; }

    public bool IsSystem { get; set; }
}

public class UpsertSettingRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    [StringLength(20)]
    public string? ValueType { get; set; }

    [StringLength(50)]
    public string? Category { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }
}
