using System;
using System.Threading;
using System.Threading.Tasks;

namespace iNetOMeter;

public enum ConnectivityState
{
    Unknown,
    Connected,
    Disconnected
}

public enum NotificationType
{
    InternetDisconnected,
    InternetRestored,
    SlowSpeed
}

public interface IConnectivityProbe
{
    Task<bool> CheckInternetReachabilityAsync(CancellationToken ct);
}

public interface INotificationService
{
    void ShowNotification(string title, string message, NotificationType type);
    bool CanShowNotification(NotificationType type);
}
