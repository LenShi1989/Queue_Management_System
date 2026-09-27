using Queue.Domain.Enums;

namespace Queue.Domain.Common;

/// <summary>
/// 當前操作者資訊（Audit Log / 冪等性識別）。由 API 層從 Claims 建立。
/// </summary>
public interface ICurrentUser
{
    string? UserId { get; }

    string? UserName { get; }

    string? DisplayName { get; }

    long? CounterId { get; }

    IReadOnlyCollection<string> Roles { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    bool IsInRole(string role);
}

public sealed class CurrentUserContext : ICurrentUser
{
    public string? UserId { get; init; }

    public string? UserName { get; init; }

    public string? DisplayName { get; init; }

    public long? CounterId { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = [];

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 可交易的工作單元抽象，讓 Application 服務不依賴 EF Core（可用於單元測試）。
/// 交易必須以 ExecuteInTransactionAsync 包覆，才能與 EF Core 的 Execution Strategy 相容。
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 以 Execution Strategy 執行一個交易單元（含自動重試）。
    /// 委派必須可安全重試：失敗時交易會完整回滾，重試不會產生部分寫入。
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 時間來源抽象，便於測試預估等待時間。
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateTimeOffset Now { get; }

    DateOnly Today { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTimeOffset Now => DateTimeOffset.Now;

    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}

public static class RolesExtensions
{
    public static bool IsAdmin(this ICurrentUser user) => user.IsInRole(QueueRoles.Admin);

    public static bool IsManagerOrAbove(this ICurrentUser user)
        => user.IsInRole(QueueRoles.Admin) || user.IsInRole(QueueRoles.Manager);
}
