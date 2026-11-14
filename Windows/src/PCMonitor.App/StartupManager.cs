using Microsoft.Win32;
using Windows.ApplicationModel;

namespace PCMonitor.App;

internal static class StartupManager
{
    private const string StartupTaskId = "PCMonitorProStartupTask";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "PCMonitorPro";

    public static async Task<StartupUpdateResult> SetEnabledAsync(bool enabled)
    {
        if (IsPackaged())
        {
            try
            {
                var task = await StartupTask.GetAsync(StartupTaskId);
                if (!enabled)
                {
                    task.Disable();
                    return new StartupUpdateResult(false, "Le démarrage avec Windows est désactivé.");
                }

                if (task.State == StartupTaskState.Disabled)
                {
                    var requestedState = await task.RequestEnableAsync();
                    return requestedState == StartupTaskState.Enabled
                        ? new StartupUpdateResult(true, "PC Monitor Pro Premium démarrera avec Windows.")
                        : new StartupUpdateResult(false, "Windows a désactivé ce démarrage. Vérifie Paramètres > Applications > Démarrage.");
                }

                return task.State == StartupTaskState.Enabled
                    ? new StartupUpdateResult(true, "PC Monitor Pro Premium démarrera avec Windows.")
                    : new StartupUpdateResult(false, "Windows a désactivé ce démarrage. Vérifie Paramètres > Applications > Démarrage.");
            }
            catch (Exception)
            {
                return new StartupUpdateResult(false, "Windows n’a pas pu modifier le démarrage automatique.");
            }
        }

        try
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (runKey is null)
            {
                return new StartupUpdateResult(false, "Windows n’a pas pu modifier le démarrage automatique.");
            }

            if (enabled)
            {
                var executablePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    return new StartupUpdateResult(false, "Le chemin de l’application est indisponible.");
                }

                runKey.SetValue(RunValueName, $"\"{executablePath}\"", RegistryValueKind.String);
                return new StartupUpdateResult(true, "PC Monitor Pro Premium démarrera avec Windows.");
            }

            runKey.DeleteValue(RunValueName, throwOnMissingValue: false);
            return new StartupUpdateResult(false, "Le démarrage avec Windows est désactivé.");
        }
        catch (Exception)
        {
            return new StartupUpdateResult(false, "Windows n’a pas pu modifier le démarrage automatique.");
        }
    }

    public static async Task<StartupUpdateResult> GetStateAsync()
    {
        if (IsPackaged())
        {
            try
            {
                var task = await StartupTask.GetAsync(StartupTaskId);
                return task.State == StartupTaskState.Enabled
                    ? new StartupUpdateResult(true, "PC Monitor Pro Premium démarrera avec Windows.")
                    : new StartupUpdateResult(false, "Le démarrage est désactivé dans Windows.");
            }
            catch (Exception)
            {
                return new StartupUpdateResult(false, "L’état du démarrage automatique est indisponible.");
            }
        }

        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var configuredPath = runKey?.GetValue(RunValueName)?.ToString();
            var executablePath = Environment.ProcessPath;
            var enabled = !string.IsNullOrWhiteSpace(configuredPath) &&
                !string.IsNullOrWhiteSpace(executablePath) &&
                string.Equals(configuredPath.Trim('"'), executablePath, StringComparison.OrdinalIgnoreCase);
            return enabled
                ? new StartupUpdateResult(true, "PC Monitor Pro Premium démarrera avec Windows.")
                : new StartupUpdateResult(false, "Le démarrage avec Windows est désactivé.");
        }
        catch (Exception)
        {
            return new StartupUpdateResult(false, "L’état du démarrage automatique est indisponible.");
        }
    }

    private static bool IsPackaged()
    {
        try
        {
            return !string.IsNullOrWhiteSpace(Package.Current.Id.FamilyName);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

internal sealed record StartupUpdateResult(bool Enabled, string Message);
