using System.Text.Json;

namespace PCMonitor.App;

internal sealed class AppSettings
{
    private const int DefaultRefreshSeconds = 2;
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PCMonitorPro",
        "settings.json");

    public int RefreshIntervalSeconds
    {
        get
        {
            var value = Load().RefreshIntervalSeconds;
            return value is 1 or 2 or 5 or 10 ? value : DefaultRefreshSeconds;
        }
        set
        {
            if (value is 1 or 2 or 5 or 10)
            {
                Save(Load() with { RefreshIntervalSeconds = value });
            }
        }
    }

    public bool MinimizeToNotificationArea
    {
        get => Load().MinimizeToNotificationArea ?? true;
        set => Save(Load() with { MinimizeToNotificationArea = value });
    }

    public bool StartWithWindows
    {
        get => Load().StartWithWindows ?? true;
        set => Save(Load() with { StartWithWindows = value });
    }

    public AppThemeChoice Theme
    {
        get => Enum.TryParse<AppThemeChoice>(Load().Theme, ignoreCase: true, out var theme)
            ? theme
            : AppThemeChoice.Automatic;
        set => Save(Load() with { Theme = value.ToString() });
    }

    public AlertPreferences Alerts
    {
        get
        {
            var settings = Load();
            return new AlertPreferences(
                settings.AlertsEnabled,
                settings.CpuAlertEnabled,
                NormalizeThreshold(settings.CpuThreshold, 85),
                settings.MemoryAlertEnabled,
                NormalizeThreshold(settings.MemoryThreshold, 85),
                settings.DiskAlertEnabled,
                NormalizeThreshold(settings.DiskThreshold, 90),
                NormalizeChoice(settings.AlertDelaySeconds, [0, 10, 30], 10),
                NormalizeChoice(settings.AlertCooldownMinutes, [5, 15, 60], 15),
                settings.QuietHoursEnabled);
        }
        set
        {
            Save(Load() with
            {
                AlertsEnabled = value.Enabled,
                CpuAlertEnabled = value.CpuEnabled,
                CpuThreshold = NormalizeThreshold(value.CpuThreshold, 85),
                MemoryAlertEnabled = value.MemoryEnabled,
                MemoryThreshold = NormalizeThreshold(value.MemoryThreshold, 85),
                DiskAlertEnabled = value.DiskEnabled,
                DiskThreshold = NormalizeThreshold(value.DiskThreshold, 90),
                AlertDelaySeconds = NormalizeChoice(value.TriggerDelaySeconds, [0, 10, 30], 10),
                AlertCooldownMinutes = NormalizeChoice(value.CooldownMinutes, [5, 15, 60], 15),
                QuietHoursEnabled = value.QuietHoursEnabled
            });
        }
    }

    private StoredSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                return JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(_settingsPath))
                    ?? new StoredSettings();
            }
        }
        catch (IOException)
        {
            // Use safe defaults if the preference file is temporarily unavailable.
        }
        catch (UnauthorizedAccessException)
        {
            // Metrics remain usable even when settings cannot be read.
        }
        catch (JsonException)
        {
            // Ignore malformed preferences and fall back to safe defaults.
        }

        return new StoredSettings();
    }

    private void Save(StoredSettings settings)
    {
        try
        {
            var settingsDirectory = Path.GetDirectoryName(_settingsPath);
            if (settingsDirectory is not null)
            {
                Directory.CreateDirectory(settingsDirectory);
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings));
            }
        }
        catch (IOException)
        {
            // Keep the selected preference for this session if persistence fails.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep the selected preference for this session if persistence fails.
        }
    }

    private static int NormalizeThreshold(int value, int fallback) => value is >= 50 and <= 99 ? value : fallback;

    private static int NormalizeChoice(int value, int[] allowed, int fallback) =>
        allowed.Contains(value) ? value : fallback;

    private sealed record StoredSettings
    {
        public int RefreshIntervalSeconds { get; init; } = DefaultRefreshSeconds;
        public bool? MinimizeToNotificationArea { get; init; }
        public bool? StartWithWindows { get; init; }
        public string? Theme { get; init; }
        public bool AlertsEnabled { get; init; }
        public bool CpuAlertEnabled { get; init; } = true;
        public int CpuThreshold { get; init; } = 85;
        public bool MemoryAlertEnabled { get; init; } = true;
        public int MemoryThreshold { get; init; } = 85;
        public bool DiskAlertEnabled { get; init; } = true;
        public int DiskThreshold { get; init; } = 90;
        public int AlertDelaySeconds { get; init; } = 10;
        public int AlertCooldownMinutes { get; init; } = 15;
        public bool QuietHoursEnabled { get; init; }
    }
}

internal enum AppThemeChoice
{
    Automatic,
    Light,
    Dark
}

internal sealed record AlertPreferences(
    bool Enabled,
    bool CpuEnabled,
    int CpuThreshold,
    bool MemoryEnabled,
    int MemoryThreshold,
    bool DiskEnabled,
    int DiskThreshold,
    int TriggerDelaySeconds = 10,
    int CooldownMinutes = 15,
    bool QuietHoursEnabled = false);
