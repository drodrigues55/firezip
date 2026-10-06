namespace Firezip.Windows.Notifications;

public interface IWindowsNotificationService
{
    event Action? NotificationInvoked;

    bool Initialize();

    bool TryShow(string title, string message);

    void Shutdown();
}
