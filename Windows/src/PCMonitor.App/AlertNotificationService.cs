using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace PCMonitor.App;

internal sealed class AlertNotificationService
{
    private bool _isRegistered;

    public event Action? NotificationInvoked;

    public bool Register()
    {
        if (_isRegistered)
        {
            return true;
        }

        try
        {
            var manager = AppNotificationManager.Default;
            manager.NotificationInvoked += OnNotificationInvoked;
            manager.Register();
            _isRegistered = true;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool Show(AlertMessage alert)
    {
        if (!_isRegistered && !Register())
        {
            return false;
        }

        try
        {
            var notification = new AppNotificationBuilder()
                .AddText($"PC Monitor Pro Premium · {alert.Title}")
                .AddText(alert.Message)
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }

        try
        {
            AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
            AppNotificationManager.Default.Unregister();
        }
        catch (Exception)
        {
            // Cleanup is best effort during application shutdown.
        }
        finally
        {
            _isRegistered = false;
        }
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        NotificationInvoked?.Invoke();
    }
}
