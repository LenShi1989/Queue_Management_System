using Microsoft.AspNetCore.SignalR;
using Queue.Application.Abstractions;

namespace Queue.Api.Hubs;

/// <summary>
/// 即時推播 Hub（§13 /hubs/queue）
/// Server → Client：QueueCreated / QueueCalled / QueueRecalled / QueueStarted /
///                 QueueCompleted / QueueNoShow / QueueCancelled / QueueTransferred /
///                 QueueUpdated / DisplayUpdated
/// </summary>
public class QueueHub : Hub
{
    public const string Route = "/hubs/queue";

    public static class Methods
    {
        public const string QueueCreated = "QueueCreated";
        public const string QueueCalled = "QueueCalled";
        public const string QueueRecalled = "QueueRecalled";
        public const string QueueStarted = "QueueStarted";
        public const string QueueCompleted = "QueueCompleted";
        public const string QueueNoShow = "QueueNoShow";
        public const string QueueCancelled = "QueueCancelled";
        public const string QueueTransferred = "QueueTransferred";
        public const string QueueUpdated = "QueueUpdated";
        public const string DisplayUpdated = "DisplayUpdated";
    }

    /// <summary>訂閱所有叫號事件（顯示器／手機頁面進入時呼叫）。</summary>
    public Task SubscribeDisplay() => Groups.AddToGroupAsync(Context.ConnectionId, "display");

    public Task UnsubscribeDisplay() => Groups.RemoveFromGroupAsync(Context.ConnectionId, "display");

    /// <summary>訂閱指定服務類型的叫號事件（顯示器／手機可依需求過濾）。</summary>
    public Task SubscribeService(string serviceCode)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"service:{serviceCode}");

    public Task UnsubscribeService(string serviceCode)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"service:{serviceCode}");

    public Task SubscribeCounter(string counterNo)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"counter:{counterNo}");

    public Task UnsubscribeCounter(string counterNo)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"counter:{counterNo}");

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "display");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "display");
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// IQueueNotifier 的 SignalR 實作。Display 事件廣播給全部連線；
/// 票據事件同時送給 display 群組與對應的服務/櫃台群組。
/// </summary>
public class SignalRQueueNotifier : IQueueNotifier
{
    private readonly IHubContext<QueueHub> _hub;

    public SignalRQueueNotifier(IHubContext<QueueHub> hub)
    {
        _hub = hub;
    }

    public Task TicketCreatedAsync(TicketEventNotification n, CancellationToken ct = default)
        => SendTicketAsync(QueueHub.Methods.QueueCreated, n, ct);

    public Task TicketCalledAsync(TicketEventNotification n, CancellationToken ct = default)
        => SendTicketAsync(QueueHub.Methods.QueueCalled, n, ct);

    public Task TicketRecalledAsync(TicketEventNotification n, CancellationToken ct = default)
        => SendTicketAsync(QueueHub.Methods.QueueRecalled, n, ct);

    public Task TicketStartedAsync(TicketEventNotification n, CancellationToken ct = default)
        => SendTicketAsync(QueueHub.Methods.QueueStarted, n, ct);

    public Task TicketCompletedAsync(TicketEventNotification n, CancellationToken ct = default)
        => SendTicketAsync(QueueHub.Methods.QueueCompleted, n, ct);

    public Task TicketNoShowAsync(TicketEventNotification n, CancellationToken ct = default)
        => SendTicketAsync(QueueHub.Methods.QueueNoShow, n, ct);

    public Task TicketCancelledAsync(TicketEventNotification n, CancellationToken ct = default)
        => SendTicketAsync(QueueHub.Methods.QueueCancelled, n, ct);

    public Task TicketTransferredAsync(TicketEventNotification n, CancellationToken ct = default)
        => SendTicketAsync(QueueHub.Methods.QueueTransferred, n, ct);

    public Task TicketUpdatedAsync(TicketEventNotification n, CancellationToken ct = default)
        => SendTicketAsync(QueueHub.Methods.QueueUpdated, n, ct);

    public Task DisplayUpdatedAsync(DisplaySnapshot snapshot, CancellationToken ct = default)
        => _hub.Clients.Group("display").SendAsync(QueueHub.Methods.DisplayUpdated, snapshot, ct);

    private Task SendTicketAsync(string method, TicketEventNotification notification, CancellationToken ct)
    {
        var display = _hub.Clients.Group("display").SendAsync(method, notification, ct);

        // 群組名稱必須與 SubscribeService(serviceCode) 一致，故使用 ServiceCode 而非 ServiceName
        if (notification.ServiceCode is { Length: > 0 } serviceCode)
        {
            var serviceGroup = _hub.Clients.Group($"service:{serviceCode}").SendAsync(method, notification, ct);
            if (notification.CounterNo is { Length: > 0 } counterNo)
            {
                var counterGroup = _hub.Clients.Group($"counter:{counterNo}").SendAsync(method, notification, ct);
                return Task.WhenAll(display, serviceGroup, counterGroup);
            }

            return Task.WhenAll(display, serviceGroup);
        }

        return display;
    }
}
