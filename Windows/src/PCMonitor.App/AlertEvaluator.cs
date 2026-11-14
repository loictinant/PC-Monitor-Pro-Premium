using PCMonitor.Core;

namespace PCMonitor.App;

internal sealed class AlertEvaluator
{
    private const double RearmMarginPercent = 5;
    private readonly HashSet<string> _activeBreaches = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AlertMessage> Evaluate(MetricSnapshot snapshot, AlertPreferences preferences)
    {
        if (!preferences.Enabled)
        {
            _activeBreaches.Clear();
            return [];
        }

        var alerts = new List<AlertMessage>();
        EvaluateMetric(
            "cpu",
            "Processeur",
            snapshot.CpuUsagePercent,
            preferences.CpuEnabled,
            preferences.CpuThreshold,
            alerts);
        EvaluateMetric(
            "memory",
            "Mémoire vive",
            snapshot.MemoryUsagePercent,
            preferences.MemoryEnabled,
            preferences.MemoryThreshold,
            alerts);

        if (preferences.DiskEnabled)
        {
            var currentDiskKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var disk in snapshot.Disks)
            {
                var key = $"disk:{disk.Name}";
                currentDiskKeys.Add(key);
                EvaluateMetric(
                    key,
                    $"Disque {disk.Name}",
                    disk.UsedPercent,
                    true,
                    preferences.DiskThreshold,
                    alerts);
            }

            _activeBreaches.RemoveWhere(key => key.StartsWith("disk:", StringComparison.OrdinalIgnoreCase) && !currentDiskKeys.Contains(key));
        }
        else
        {
            _activeBreaches.RemoveWhere(key => key.StartsWith("disk:", StringComparison.OrdinalIgnoreCase));
        }

        return alerts;
    }

    private void EvaluateMetric(
        string key,
        string label,
        double? value,
        bool enabled,
        int threshold,
        ICollection<AlertMessage> alerts)
    {
        if (!enabled)
        {
            _activeBreaches.Remove(key);
            return;
        }

        if (value is not double measuredValue)
        {
            return;
        }

        if (measuredValue < threshold - RearmMarginPercent)
        {
            _activeBreaches.Remove(key);
            return;
        }

        if (measuredValue >= threshold && _activeBreaches.Add(key))
        {
            alerts.Add(new AlertMessage(label, $"{label} dépasse {threshold} % (mesure : {measuredValue:0} %)."));
        }
    }
}

internal sealed record AlertMessage(string Title, string Message);
