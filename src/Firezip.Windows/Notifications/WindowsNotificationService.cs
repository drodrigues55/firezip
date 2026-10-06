using Firezip.Core.Interfaces;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Firezip.Windows.Notifications;

public sealed class WindowsNotificationService(ILoggingService loggingService) : IWindowsNotificationService
{
    private AppNotificationManager? _manager;

    public event Action? NotificationInvoked;

    public bool Initialize()
    {
        if (_manager != null)
            return true;

        try
        {
            _manager = AppNotificationManager.Default;
            _manager.NotificationInvoked += OnNotificationInvoked;
            _manager.Register();
            return true;
        }
        catch (Exception ex)
        {
            if (_manager != null)
            {
                try
                {
                    _manager.NotificationInvoked -= OnNotificationInvoked;
                }
                catch
                {
                    // Keep notifications optional if Windows registration is unavailable.
                }

                _manager = null;
            }

            loggingService.Warn($"Windows notifications are unavailable ({ex.GetType().Name}).");
            return false;
        }
    }

    public bool TryShow(string title, string message)
    {
        if (_manager == null || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            return false;

        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message)
                .BuildNotification();
            _manager.Show(notification);
            return true;
        }
        catch (Exception ex)
        {
            loggingService.Warn($"Could not show a Windows notification ({ex.GetType().Name}).");
            return false;
        }
    }

    public void Shutdown()
    {
        if (_manager == null)
            return;

        try
        {
            _manager.NotificationInvoked -= OnNotificationInvoked;
            _manager.Unregister();
        }
        catch (Exception ex)
        {
            loggingService.Warn($"Could not unregister Windows notifications ({ex.GetType().Name}).");
        }
        finally
        {
            _manager = null;
        }
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        NotificationInvoked?.Invoke();
    }
}
