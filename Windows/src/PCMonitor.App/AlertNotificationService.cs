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
            manager.NotificationInvoked -= OnNotificationInvoked;
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
            var manager = AppNotificationManager.Default;
            if (manager.Setting != AppNotificationSetting.Enabled)
            {
                return false;
            }

            var notification = new AppNotificationBuilder()
                .AddText($"PC Monitor Pro Premium · {alert.Title}")
                .AddText(alert.Message)
                .BuildNotification();

            manager.Show(notification);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public NotificationTestResult SendTestNotification()
    {
        if (!Register())
        {
            return new(
                false,
                false,
                "Windows n’a pas pu enregistrer PC Monitor Pro pour les notifications. Vérifie que l’application n’est pas lancée en tant qu’administrateur, puis réessaie.");
        }

        try
        {
            var manager = AppNotificationManager.Default;
            if (manager.Setting != AppNotificationSetting.Enabled)
            {
                return new(false, true, GetDisabledMessage(manager.Setting));
            }

            var notification = new AppNotificationBuilder()
                .AddText("PC Monitor Pro Premium — test")
                .AddText("Les notifications Windows sont prêtes. Cette notification confirme leur fonctionnement.")
                .BuildNotification();
            manager.Show(notification);

            return new(
                true,
                false,
                "Une notification de test a été envoyée à Windows. Si elle n’apparaît pas, vérifie les bannières et le mode Ne pas déranger dans les paramètres Windows.");
        }
        catch (Exception)
        {
            return new(
                false,
                false,
                "Windows n’a pas accepté la notification de test. Ouvre les paramètres de notifications et vérifie que PC Monitor Pro Premium est autorisé.");
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

    private static string GetDisabledMessage(AppNotificationSetting setting) => setting switch
    {
        AppNotificationSetting.DisabledForApplication => "Les notifications sont désactivées pour PC Monitor Pro Premium. Active-les dans Paramètres Windows > Système > Notifications.",
        AppNotificationSetting.DisabledForUser => "Les notifications Windows sont désactivées pour cette session. Active-les dans Paramètres Windows > Système > Notifications.",
        AppNotificationSetting.DisabledByGroupPolicy => "Une stratégie Windows bloque les notifications. Un administrateur doit modifier ce réglage.",
        AppNotificationSetting.DisabledByManifest => "Le manifeste de l’application bloque les notifications. Cette version doit être corrigée avant de pouvoir les activer.",
        _ => "Les notifications Windows ne sont pas disponibles pour PC Monitor Pro Premium. Vérifie Paramètres Windows > Système > Notifications."
    };
}

internal sealed record NotificationTestResult(bool Sent, bool NeedsSettings, string Message);
