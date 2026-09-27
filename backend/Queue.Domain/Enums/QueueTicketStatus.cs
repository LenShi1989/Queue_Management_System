namespace Queue.Domain.Enums;

/// <summary>
/// 排隊票據狀態
/// Waiting → Calling → Serving → Completed
/// 異常：Waiting→Cancelled、Calling→NoShow、Serving→Transferred
/// </summary>
public enum QueueTicketStatus
{
    Waiting = 1,
    Calling = 2,
    Serving = 3,
    Completed = 4,
    NoShow = 5,
    Cancelled = 6,
    Transferred = 7,
    Suspended = 8
}

/// <summary>
/// 櫃台狀態
/// </summary>
public enum QueueCounterStatus
{
    Idle = 1,
    Busy = 2,
    Paused = 3,
    Offline = 4
}

/// <summary>
/// 票據歷史動作
/// </summary>
public enum QueueTicketAction
{
    Create = 1,
    Call = 2,
    Recall = 3,
    Start = 4,
    Complete = 5,
    NoShow = 6,
    Cancel = 7,
    Transfer = 8,
    Suspend = 9,
    Resume = 10
}

/// <summary>
/// 系統角色
/// </summary>
public static class QueueRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Counter = "Counter";
    public const string Display = "Display";
    public const string Kiosk = "Kiosk";
    public const string Mobile = "Mobile";

    public static readonly string[] All = [Admin, Manager, Counter, Display, Kiosk, Mobile];
}
