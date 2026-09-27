using Queue.Domain.Enums;
using Queue.Domain.Exceptions;

namespace Queue.Tests;

/// <summary>
/// 驗證規格 §44.1「Queue Ticket 狀態機」與 §6 角色定義。
/// </summary>
public class QueueStatusTransitionsTests
{
    [Theory]
    // §44.1 正常流程
    [InlineData(QueueTicketStatus.Waiting, QueueTicketStatus.Calling)]
    [InlineData(QueueTicketStatus.Calling, QueueTicketStatus.Serving)]
    [InlineData(QueueTicketStatus.Serving, QueueTicketStatus.Completed)]
    // §44.1 異常流程
    [InlineData(QueueTicketStatus.Waiting, QueueTicketStatus.Cancelled)]
    [InlineData(QueueTicketStatus.Calling, QueueTicketStatus.NoShow)]
    [InlineData(QueueTicketStatus.Serving, QueueTicketStatus.Transferred)]
    // 再叫 / 插隊
    [InlineData(QueueTicketStatus.Calling, QueueTicketStatus.Calling)]
    [InlineData(QueueTicketStatus.Serving, QueueTicketStatus.Calling)]
    [InlineData(QueueTicketStatus.Calling, QueueTicketStatus.Waiting)]
    [InlineData(QueueTicketStatus.Suspended, QueueTicketStatus.Waiting)]
    [InlineData(QueueTicketStatus.Suspended, QueueTicketStatus.Calling)]
    public void CanTransition_允許的狀態流應為true(QueueTicketStatus from, QueueTicketStatus to)
    {
        Assert.True(QueueStatusTransitions.CanTransition(from, to));
        QueueStatusTransitions.EnsureCanTransition(from, to);
    }

    [Theory]
    [InlineData(QueueTicketStatus.Completed, QueueTicketStatus.Waiting)]
    [InlineData(QueueTicketStatus.Completed, QueueTicketStatus.Calling)]
    [InlineData(QueueTicketStatus.Cancelled, QueueTicketStatus.Waiting)]
    [InlineData(QueueTicketStatus.Transferred, QueueTicketStatus.Serving)]
    [InlineData(QueueTicketStatus.Waiting, QueueTicketStatus.Serving)]
    [InlineData(QueueTicketStatus.Waiting, QueueTicketStatus.Completed)]
    public void CanTransition_不允許的狀態流應為false(QueueTicketStatus from, QueueTicketStatus to)
    {
        Assert.False(QueueStatusTransitions.CanTransition(from, to));
    }

    [Fact]
    public void EnsureCanTransition_非法流應拋出INVALID_STATUS_TRANSITION()
    {
        var ex = Assert.Throws<DomainException>(() =>
            QueueStatusTransitions.EnsureCanTransition(QueueTicketStatus.Waiting, QueueTicketStatus.Serving));

        Assert.Equal("INVALID_STATUS_TRANSITION", ex.Code);
    }

    [Theory]
    [InlineData(QueueTicketStatus.Waiting, true)]
    [InlineData(QueueTicketStatus.Calling, true)]
    [InlineData(QueueTicketStatus.Serving, true)]
    [InlineData(QueueTicketStatus.Suspended, true)]
    [InlineData(QueueTicketStatus.Completed, false)]
    [InlineData(QueueTicketStatus.Cancelled, false)]
    [InlineData(QueueTicketStatus.NoShow, false)]
    [InlineData(QueueTicketStatus.Transferred, false)]
    public void IsInQueue_應符合是否仍在佇列中(QueueTicketStatus status, bool expected)
        => Assert.Equal(expected, QueueStatusTransitions.IsInQueue(status));

    [Theory]
    [InlineData(QueueTicketStatus.Completed, true)]
    [InlineData(QueueTicketStatus.Cancelled, true)]
    [InlineData(QueueTicketStatus.Waiting, false)]
    [InlineData(QueueTicketStatus.NoShow, false)]
    public void IsTerminal_終態判斷正確(QueueTicketStatus status, bool expected)
        => Assert.Equal(expected, QueueStatusTransitions.IsTerminal(status));

    [Fact]
    public void AllowedTargets_Completed不應有任何後續狀態()
        => Assert.Empty(QueueStatusTransitions.AllowedTargets(QueueTicketStatus.Completed));

    [Fact]
    public void Roles_應包含規格定義的六種角色()
    {
        Assert.Equal(6, QueueRoles.All.Length);
        Assert.Contains(QueueRoles.Admin, QueueRoles.All);
        Assert.Contains(QueueRoles.Manager, QueueRoles.All);
        Assert.Contains(QueueRoles.Counter, QueueRoles.All);
        Assert.Contains(QueueRoles.Display, QueueRoles.All);
        Assert.Contains(QueueRoles.Kiosk, QueueRoles.All);
        Assert.Contains(QueueRoles.Mobile, QueueRoles.All);
    }
}
