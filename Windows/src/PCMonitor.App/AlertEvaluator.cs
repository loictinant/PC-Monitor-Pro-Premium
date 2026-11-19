using PCMonitor.Core;

namespace PCMonitor.App;

internal sealed class AlertEvaluator
{
    private const double RearmMarginPercent = 5;
    private readonly Dictionary<string, BreachState> _states = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AlertMessage> Evaluate(MetricSnapshot snapshot, AlertPreferences preferences)
    {
        if (!preferences.Enabled)
        {
            _states.Clear();
            return [];
        }

        var alerts = new List<AlertMessage>();
        EvaluateMetric("cpu", "Processeur", snapshot.CpuUsagePercent, preferences.CpuEnabled, preferences.CpuThreshold, snapshot.CapturedAt, preferences, alerts);
        EvaluateMetric("memory", "Mémoire vive", snapshot.MemoryUsagePercent, preferences.MemoryEnabled, preferences.MemoryThreshold, snapshot.CapturedAt, preferences, alerts);

        if (preferences.DiskEnabled)
        {
            var currentDiskKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var disk in snapshot.Disks)
            {
                var key = $"disk:{disk.Name}";
                currentDiskKeys.Add(key);
                EvaluateMetric(key, $"Disque {disk.Name}", disk.UsedPercent, true, preferences.DiskThreshold, snapshot.CapturedAt, preferences, alerts);
            }

            foreach (var staleKey in _states.Keys.Where(key => key.StartsWith("disk:", StringComparison.OrdinalIgnoreCase) && !currentDiskKeys.Contains(key)).ToArray())
            {
                _states.Remove(staleKey);
            }
        }
        else
        {
            RemoveKeys("disk:");
        }

        return alerts;
    }

    private void EvaluateMetric(
        string key,
        string label,
        double? value,
        bool enabled,
        int threshold,
        DateTimeOffset capturedAt,
        AlertPreferences preferences,
        ICollection<AlertMessage> alerts)
    {
        if (!enabled)
        {
            _states.Remove(key);
            return;
        }

        if (value is not double measuredValue)
        {
            return;
        }

        if (measuredValue < threshold - RearmMarginPercent)
        {
            _states.Remove(key);
            return;
        }

        if (!_states.TryGetValue(key, out var state))
        {
            state = new BreachState(capturedAt, null);
            _states[key] = state;
        }

        if (measuredValue < threshold || capturedAt - state.StartedAt < TimeSpan.FromSeconds(preferences.TriggerDelaySeconds))
        {
            return;
        }

        var cooldown = TimeSpan.FromMinutes(preferences.CooldownMinutes);
        if (state.LastAlertAt is DateTimeOffset lastAlert && capturedAt - lastAlert < cooldown)
        {
            return;
        }

        if (preferences.QuietHoursEnabled && IsQuietHours(capturedAt))
        {
            return;
        }

        alerts.Add(new AlertMessage(label, $"{label} dépasse {threshold} % (mesure : {measuredValue:0} %)."));
        _states[key] = state with { LastAlertAt = capturedAt };
    }

    private void RemoveKeys(string prefix)
    {
        foreach (var key in _states.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            _states.Remove(key);
        }
    }

    private static bool IsQuietHours(DateTimeOffset timestamp)
    {
        var localTime = TimeOnly.FromDateTime(timestamp.ToLocalTime().DateTime);
        return localTime >= new TimeOnly(22, 0) || localTime < new TimeOnly(7, 0);
    }

    private sealed record BreachState(DateTimeOffset StartedAt, DateTimeOffset? LastAlertAt);
}

internal sealed record AlertMessage(string Title, string Message);
