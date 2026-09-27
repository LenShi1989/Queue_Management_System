using Queue.Domain.Enums;

namespace Queue.Domain.Exceptions;

/// <summary>
/// 業務規則例外。Code 會直接回傳給前端（統一錯誤格式 §32）。
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}

/// <summary>
/// 允許的狀態轉換驗證（§6 / §44.1 Queue Ticket 與 Queue Status 分離）
/// </summary>
public static class QueueStatusTransitions
{
    private static readonly Dictionary<QueueTicketStatus, QueueTicketStatus[]> Allowed =
        new()
        {
            [QueueTicketStatus.Waiting] = [QueueTicketStatus.Calling, QueueTicketStatus.Cancelled],
            [QueueTicketStatus.Calling] = [
                QueueTicketStatus.Serving,
                QueueTicketStatus.NoShow,
                QueueTicketStatus.Calling,   // 再叫一次
                QueueTicketStatus.Waiting,    // 叫號逾時回到等待
                QueueTicketStatus.Cancelled
            ],
            [QueueTicketStatus.Serving] = [
                QueueTicketStatus.Completed,
                QueueTicketStatus.Transferred,
                QueueTicketStatus.Calling
            ],
            [QueueTicketStatus.Suspended] = [QueueTicketStatus.Waiting, QueueTicketStatus.Calling, QueueTicketStatus.Cancelled],
            [QueueTicketStatus.Completed] = [],
            [QueueTicketStatus.NoShow] = [QueueTicketStatus.Waiting],
            [QueueTicketStatus.Cancelled] = [],
            [QueueTicketStatus.Transferred] = []
        };

    public static bool CanTransition(QueueTicketStatus from, QueueTicketStatus to)
        => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyList<QueueTicketStatus> AllowedTargets(QueueTicketStatus from)
        => Allowed.TryGetValue(from, out var targets) ? targets : [];

    public static void EnsureCanTransition(QueueTicketStatus from, QueueTicketStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new DomainException(
                "INVALID_STATUS_TRANSITION",
                $"不允許的狀態轉換：{from} → {to}");
        }
    }

    /// <summary>是否為終結狀態（不可再異動）。</summary>
    public static bool IsTerminal(QueueTicketStatus status)
        => status is QueueTicketStatus.Completed or QueueTicketStatus.Cancelled;

    /// <summary>是否仍佔用佇列位置。</summary>
    public static bool IsInQueue(QueueTicketStatus status)
        => status is QueueTicketStatus.Waiting or QueueTicketStatus.Calling or QueueTicketStatus.Serving or QueueTicketStatus.Suspended;
}
