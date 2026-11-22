using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using H.NotifyIcon;
using PCMonitor.Core;
using ProductLicensing.Abstractions;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;

namespace PCMonitor.App;

public sealed partial class MainWindow : Window
{
    private const int MaximumHistorySamples = 900;
    private static readonly TimeSpan SessionChartDuration = TimeSpan.FromMinutes(15);
    private readonly IMetricsCollector _metricsCollector = new WindowsMetricsCollector();
    private readonly MetricsHistory _history = new();
    private readonly PersistentMetricsHistory _premiumHistory = new();
    private readonly AppSettings _settings = new();
    private readonly AlertEvaluator _alertEvaluator = new();
    private readonly AlertNotificationService _alertNotifications = new();
    private readonly ProductFeatureGate _featureGate = new();
    private readonly MicrosoftStoreLicenseProvider _licenseProvider;
    private readonly DispatcherQueueTimer _timer;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly TaskbarIcon _trayIcon;
    private bool _isPaused;
    private bool _isUpdatingInterval;
    private bool _isUpdatingTrayPreference;
    private bool _isUpdatingStartupPreference;
    private bool _isResettingProToggle;
    private bool _isUpdatingThemeChoice;
    private bool _allowApplicationClose;
    private bool _isCollecting;
    private MetricSnapshot? _lastSnapshot;
    private LicenseState _licenseState = DevelopmentLicense.Current;

    public MainWindow()
    {
        InitializeComponent();
#if PCMONITORPRO_PREMIUM_TEST
        Title = "PC Monitor Pro Premium — TEST";
#else
        Title = "PC Monitor Pro Premium";
#endif
        HistoryRangeComboBox.SelectedIndex = 2;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _licenseProvider = new MicrosoftStoreLicenseProvider(
            this,
            StoreCatalog.ProAddOnStoreId,
            StoreCatalog.ProAddOnProductId);
        ApplyTheme(_settings.Theme);
        SelectThemeChoice(_settings.Theme);
        WindowRoot.ActualThemeChanged += OnActualThemeChanged;

        _trayIcon = CreateTrayIcon();
        WindowRoot.Children.Add(_trayIcon);
        _isUpdatingTrayPreference = true;
        MinimizeToTrayToggle.IsOn = _settings.MinimizeToNotificationArea;
        _isUpdatingTrayPreference = false;
        SetTrayIconVisibility(_settings.MinimizeToNotificationArea);
        AppWindow.Changed += OnAppWindowChanged;
        AppWindow.Closing += OnAppWindowClosing;
        Closed += OnWindowClosed;

        _isUpdatingStartupPreference = true;
        StartWithWindowsToggle.IsOn = _settings.StartWithWindows;
        _isUpdatingStartupPreference = false;
        _ = SynchronizeStartupSettingAsync();

        InitializeAlertControls();
        UpdateLicenseDependentUi();
        RootNavigation.SelectedItem = RootNavigation.MenuItems.OfType<NavigationViewItem>().First();
        var startupInterval = _settings.RefreshIntervalSeconds <= 10 ? _settings.RefreshIntervalSeconds : 2;
        SelectRefreshInterval(startupInterval);

        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(startupInterval);
        _timer.IsRepeating = true;
        _timer.Tick += OnTimerTick;
        _timer.Start();

        _ = RefreshLicenseStateAsync();
        _ = RefreshMetricsAsync();
    }

    private async void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (!_isPaused)
        {
            await RefreshMetricsAsync();
        }
    }

    private async Task RefreshMetricsAsync()
    {
        if (_isCollecting)
        {
            return;
        }

        _isCollecting = true;
        try
        {
            var snapshot = await Task.Run(() => _metricsCollector.CollectAsync());
            _lastSnapshot = snapshot;
            _history.Add(snapshot, MaximumHistorySamples);
            UpdateDashboard(snapshot);
            UpdateTrayTooltip(snapshot);
            if (IsProFeatureEnabled(ProductFeatures.PersistentHistory))
            {
                await _premiumHistory.RecordAsync(snapshot);
                UpdateHistoryView();
            }
            if (IsProFeatureEnabled(ProductFeatures.Alerts))
            {
                foreach (var alert in _alertEvaluator.Evaluate(snapshot, _settings.Alerts))
                {
                    if (!_alertNotifications.Show(alert))
                    {
                        AlertsStatusText.Text = "Windows n’a pas pu afficher une notification. Vérifie les autorisations.";
                    }
                }
            }
            StatusText.Text = _isPaused ? "Surveillance en pause" : "Surveillance active";
        }
        catch (Exception)
        {
            StatusText.Text = "Mesure indisponible";
            LastUpdatedText.Text = "Une source Windows n’a pas répondu. Les prochaines mesures continueront.";
        }
        finally
        {
            _isCollecting = false;
        }
    }

    private void UpdateDashboard(MetricSnapshot snapshot)
    {
        CpuValueText.Text = snapshot.CpuUsagePercent is double cpuUsage ? $"{cpuUsage:0}%" : "—";
        CpuProgressBar.Value = snapshot.CpuUsagePercent is double cpuPercent
            ? Math.Clamp(cpuPercent, 0, 100)
            : 0;

        if (snapshot.MemoryUsagePercent is double memoryPercent &&
            snapshot.UsedPhysicalBytes is ulong usedMemory &&
            snapshot.TotalPhysicalBytes is ulong totalMemory)
        {
            MemoryValueText.Text = $"{memoryPercent:0}%";
            MemoryProgressBar.Value = Math.Clamp(memoryPercent, 0, 100);
            MemoryDetailText.Text = $"{FormatBytes(usedMemory)} utilisés sur {FormatBytes(totalMemory)}";
        }
        else
        {
            MemoryValueText.Text = "—";
            MemoryProgressBar.Value = 0;
            MemoryDetailText.Text = "Mesure indisponible";
        }

        if (snapshot.Disks.Count > 0)
        {
            var busiestDisk = snapshot.Disks.OrderByDescending(disk => disk.UsedPercent).First();
            DiskValueText.Text = $"{busiestDisk.UsedPercent:0}%";
            DiskProgressBar.Value = Math.Clamp(busiestDisk.UsedPercent, 0, 100);
            var readRate = snapshot.DiskReadBytesPerSecond is double read ? $"lecture {FormatBytesPerSecond(read)}/s" : "lecture indisponible";
            var writeRate = snapshot.DiskWriteBytesPerSecond is double write ? $"écriture {FormatBytesPerSecond(write)}/s" : "écriture indisponible";
            DiskDetailText.Text = $"{busiestDisk.Name} · {FormatBytes(busiestDisk.FreeBytes)} libres · {readRate} · {writeRate}";
        }
        else
        {
            DiskValueText.Text = "—";
            DiskProgressBar.Value = 0;
            DiskDetailText.Text = "Aucun volume fixe prêt";
        }

        DownloadValueText.Text = $"{FormatBytesPerSecond(snapshot.DownloadBytesPerSecond)} /s";
        UploadValueText.Text = $"{FormatBytesPerSecond(snapshot.UploadBytesPerSecond)} /s";
        NetworkAdaptersText.Text = snapshot.NetworkInterfaces.Count == 0
            ? "Aucun adaptateur actif"
            : string.Join("\n", snapshot.NetworkInterfaces.Take(2).Select(network =>
                $"{network.Name}: ↓ {FormatBytesPerSecond(network.DownloadBytesPerSecond)} /s · ↑ {FormatBytesPerSecond(network.UploadBytesPerSecond)} /s"));
        ChartCurrentText.Text = snapshot.CpuUsagePercent is double chartCpu ? $"{chartCpu:0}%" : "—";
        var chartSamples = _history.GetSince(DateTimeOffset.UtcNow - SessionChartDuration);
        var chartSeconds = chartSamples.Count < 2
            ? 0
            : (int)(chartSamples[^1].CapturedAt - chartSamples[0].CapturedAt).TotalSeconds;
        ChartTimeRangeText.Text = chartSeconds == 0
            ? "Collecte de l’historique…"
            : $"15 min · {FormatDuration(chartSeconds)} collectées";
        LastUpdatedText.Text = $"Dernier relevé : {snapshot.CapturedAt.ToLocalTime():HH:mm:ss} · intervalle de {_settings.RefreshIntervalSeconds} s";

        UpdateDiskList(snapshot.Disks);
        DrawCpuChart();
        UpdateHistoryView();
    }

    private void UpdateDiskList(IReadOnlyList<DiskVolumeSnapshot> disks)
    {
        DiskVolumesPanel.Children.Clear();
        DiskCountText.Text = disks.Count == 1 ? "1 lecteur" : $"{disks.Count} lecteurs";

        foreach (var disk in disks)
        {
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text = $"{disk.Name}  {disk.Label}",
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = GetThemeBrush("PrimaryTextBrush")
            };
            var capacity = new TextBlock
            {
                Text = $"{FormatBytes(disk.UsedBytes)} / {FormatBytes(disk.TotalBytes)}",
                FontSize = 11,
                Foreground = GetThemeBrush("MutedTextBrush"),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(capacity, 1);
            header.Children.Add(name);
            header.Children.Add(capacity);

            var progress = new ProgressBar
            {
                Maximum = 100,
                Value = Math.Clamp(disk.UsedPercent, 0, 100),
                Height = 5,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var detail = new TextBlock
            {
                Text = $"{disk.UsedPercent:0}% utilisé · {FormatBytes(disk.FreeBytes)} libres",
                FontSize = 11,
                Foreground = GetThemeBrush("MutedTextBrush"),
                Margin = new Thickness(0, 5, 0, 0)
            };

            var item = new StackPanel();
            item.Children.Add(header);
            item.Children.Add(progress);
            item.Children.Add(detail);
            DiskVolumesPanel.Children.Add(item);
        }

        if (disks.Count == 0)
        {
            DiskVolumesPanel.Children.Add(new TextBlock
            {
                Text = "Aucun volume à afficher.",
                Foreground = GetThemeBrush("MutedTextBrush"),
                FontSize = 13
            });
        }
    }

    private void DrawCpuChart()
    {
        CpuChartCanvas.Children.Clear();
        var width = CpuChartCanvas.ActualWidth;
        var height = CpuChartCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var samples = _history.GetSince(DateTimeOffset.UtcNow - SessionChartDuration)
            .Where(sample => sample.CpuUsagePercent.HasValue)
            .ToArray();
        for (var i = 1; i <= 3; i++)
        {
            var y = height * i / 4;
            CpuChartCanvas.Children.Add(new Line
            {
                X1 = 0,
                X2 = width,
                Y1 = y,
                Y2 = y,
                Stroke = GetThemeBrush("ChartGridBrush"),
                StrokeThickness = 1
            });
        }

        if (samples.Length < 2)
        {
            return;
        }

        var points = new PointCollection();
        for (var index = 0; index < samples.Length; index++)
        {
            var x = (float)(width * index / (samples.Length - 1));
            var y = (float)(height - Math.Clamp(samples[index].CpuUsagePercent!.Value, 0, 100) / 100 * height);
            points.Add(new Windows.Foundation.Point(x, y));
        }

        CpuChartCanvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(Color.FromArgb(255, 99, 214, 189)),
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        });
    }

    private void UpdateTrayTooltip(MetricSnapshot snapshot)
    {
        var cpu = snapshot.CpuUsagePercent is double cpuPercent ? $"CPU {cpuPercent:0}%" : "CPU —";
        var memory = snapshot.MemoryUsagePercent is double memoryPercent ? $"RAM {memoryPercent:0}%" : "RAM —";
        _trayIcon.ToolTipText = $"PC Monitor Pro · {cpu} · {memory} · ↓ {FormatBytesPerSecond(snapshot.DownloadBytesPerSecond)}/s";
    }

    private TimeSpan GetHistoryRange()
    {
        var hours = GetSelectedTag(HistoryRangeComboBox, 24);
        return TimeSpan.FromHours(hours is 1 or 6 or 24 ? hours : 24);
    }

    private void OnHistoryRangeChanged(object sender, SelectionChangedEventArgs args) => UpdateHistoryView();

    private void UpdateHistoryView()
    {
        if (!IsProFeatureEnabled(ProductFeatures.PersistentHistory))
        {
            HistoryStatusText.Text = "Fonction incluse dans Premium (PRO). Sélectionne-la pour afficher l’offre Microsoft Store.";
            HardwareSensorsText.Text = "Les capteurs matériels sont disponibles avec Premium (PRO).";
            HistoryCpuCanvas.Children.Clear();
            HistoryMemoryCanvas.Children.Clear();
            HistoryNetworkCanvas.Children.Clear();
            HistoryDiskCanvas.Children.Clear();
            HistoryCpuSummaryText.Text = "Verrouillé (PRO)";
            HistoryMemorySummaryText.Text = "Verrouillé (PRO)";
            HistoryNetworkSummaryText.Text = "Verrouillé (PRO)";
            HistoryDiskSummaryText.Text = "Verrouillé (PRO)";
            return;
        }

        var samples = _premiumHistory.GetLatest(GetHistoryRange());
        HistoryStatusText.Text = samples.Count == 0
            ? "Collecte locale activée · les relevés sont conservés jusqu’à 24 h sur cet appareil."
            : $"{samples.Count:N0} relevés locaux · conservation maximale 24 h · dernière mesure {samples[^1].CapturedAt.ToLocalTime():HH:mm:ss}";

        SetHistorySummary(HistoryCpuSummaryText, samples, sample => sample.CpuUsagePercent);
        SetHistorySummary(HistoryMemorySummaryText, samples, sample => sample.MemoryUsagePercent);
        SetRateSummary(HistoryNetworkSummaryText, samples, sample => sample.DownloadBytesPerSecond, sample => sample.UploadBytesPerSecond);
        SetRateSummary(HistoryDiskSummaryText, samples, sample => sample.DiskReadBytesPerSecond, sample => sample.DiskWriteBytesPerSecond);
        DrawHistoryMetric(HistoryCpuCanvas, samples, sample => sample.CpuUsagePercent, Color.FromArgb(255, 99, 214, 189));
        DrawHistoryMetric(HistoryMemoryCanvas, samples, sample => sample.MemoryUsagePercent, Color.FromArgb(255, 100, 160, 255));
        DrawHistoryRates(HistoryNetworkCanvas, samples, sample => sample.DownloadBytesPerSecond, sample => sample.UploadBytesPerSecond);
        DrawHistoryRates(HistoryDiskCanvas, samples, sample => sample.DiskReadBytesPerSecond, sample => sample.DiskWriteBytesPerSecond);
        UpdateHardwareSensorSummary(_lastSnapshot?.Hardware);
    }

    private static void SetHistorySummary(TextBlock target, IReadOnlyList<MetricSample> samples, Func<MetricSample, double?> metric)
    {
        var values = samples.Select(metric).Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        target.Text = values.Length == 0 ? "En attente des relevés" : $"Moyenne {values.Average():0}% · dernier {values[^1]:0}%";
    }

    private void DrawHistoryMetric(Canvas canvas, IReadOnlyList<MetricSample> samples, Func<MetricSample, double?> metric, Color color)
    {
        canvas.Children.Clear();
        var width = canvas.ActualWidth;
        var height = canvas.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        for (var line = 1; line <= 3; line++)
        {
            var y = height * line / 4;
            canvas.Children.Add(new Line
            {
                X1 = 0,
                X2 = width,
                Y1 = y,
                Y2 = y,
                Stroke = GetThemeBrush("ChartGridBrush"),
                StrokeThickness = 1
            });
        }

        var values = samples.Select(sample => metric(sample)).ToArray();
        if (values.Count(value => value.HasValue) < 2)
        {
            return;
        }

        var points = new PointCollection();
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            if (value is null)
            {
                continue;
            }

            var x = width * index / Math.Max(1, values.Length - 1);
            var y = height - Math.Clamp(value.Value, 0, 100) / 100 * height;
            points.Add(new Windows.Foundation.Point(x, y));
        }

        canvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        });
    }

    private static void SetRateSummary(TextBlock target, IReadOnlyList<MetricSample> samples, Func<MetricSample, double?> first, Func<MetricSample, double?> second)
    {
        if (samples.Count == 0)
        {
            target.Text = "En attente des relevés";
            return;
        }

        var latest = samples[^1];
        var latestFirst = first(latest);
        var latestSecond = second(latest);
        var maximum = samples.SelectMany(sample => new[] { first(sample), second(sample) })
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty(0)
            .Max();
        target.Text = $"Max {FormatBytesPerSecond(maximum)}/s · dernier {FormatBytesPerSecond(latestFirst ?? 0)}/{FormatBytesPerSecond(latestSecond ?? 0)} /s";
    }

    private void DrawHistoryRates(
        Canvas canvas,
        IReadOnlyList<MetricSample> samples,
        Func<MetricSample, double?> first,
        Func<MetricSample, double?> second)
    {
        canvas.Children.Clear();
        var width = canvas.ActualWidth;
        var height = canvas.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        for (var line = 1; line <= 3; line++)
        {
            var y = height * line / 4;
            canvas.Children.Add(new Line
            {
                X1 = 0,
                X2 = width,
                Y1 = y,
                Y2 = y,
                Stroke = GetThemeBrush("ChartGridBrush"),
                StrokeThickness = 1
            });
        }

        var maximum = samples.SelectMany(sample => new[] { first(sample), second(sample) })
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty(0)
            .Max();
        if (maximum <= 0 || samples.Count < 2)
        {
            return;
        }

        AddRateLine(first, Color.FromArgb(255, 99, 214, 189));
        AddRateLine(second, Color.FromArgb(255, 100, 160, 255));

        void AddRateLine(Func<MetricSample, double?> selector, Color color)
        {
            var points = new PointCollection();
            for (var index = 0; index < samples.Count; index++)
            {
                var value = selector(samples[index]);
                if (value is null)
                {
                    continue;
                }

                var x = width * index / Math.Max(1, samples.Count - 1);
                var y = height - Math.Clamp(value.Value / maximum, 0, 1) * height;
                points.Add(new Windows.Foundation.Point(x, y));
            }

            if (points.Count >= 2)
            {
                canvas.Children.Add(new Polyline
                {
                    Points = points,
                    Stroke = new SolidColorBrush(color),
                    StrokeThickness = 2.5,
                    StrokeLineJoin = PenLineJoin.Round
                });
            }
        }
    }

    private void UpdateHardwareSensorSummary(HardwareMetricsSnapshot? hardware)
    {
        if (!IsProFeatureEnabled(ProductFeatures.HardwareSensors))
        {
            HardwareSensorsText.Text = "Fonction incluse dans Premium (PRO).";
            return;
        }

        var details = new List<string>();
        if (hardware?.GpuLoadPercent is double gpuLoad)
        {
            details.Add($"GPU {hardware.GpuName ?? ""} · charge {gpuLoad:0}%");
        }
        else
        {
            details.Add($"GPU : {hardware?.GpuName ?? "capteur non disponible sur ce PC"} · charge non disponible");
        }

        if (hardware?.GpuMemoryUsedBytes is ulong gpuUsed)
        {
            var gpuMemory = hardware.GpuMemoryTotalBytes is ulong gpuTotal
                ? $"{FormatBytes(gpuUsed)} / {FormatBytes(gpuTotal)}"
                : FormatBytes(gpuUsed);
            details.Add($"Mémoire vidéo : {gpuMemory}");
        }
        else
        {
            details.Add("Mémoire vidéo : capteur non disponible");
        }

        details.Add(hardware?.GpuTemperatureCelsius is double gpuTemp ? $"Température GPU : {gpuTemp:0} °C" : "Température GPU : capteur non disponible");
        details.Add(hardware?.CpuTemperatureCelsius is double cpuTemp ? $"Température CPU : {cpuTemp:0} °C" : "Température CPU : capteur non disponible");
        details.Add(hardware?.FanSpeedRpm is double fan ? $"Ventilateur : {fan:0} tr/min" : "Ventilateur : capteur non disponible");
        HardwareSensorsText.Text = string.Join("\n", details);
    }

    private async void OnExportCsvClicked(object sender, RoutedEventArgs args) => await ExportHistoryCsvAsync();

    private async void OnGeneratePerformanceReportClicked(object sender, RoutedEventArgs args) => await GeneratePerformanceReportAsync();

    private async Task ExportHistoryCsvAsync()
    {
        if (!IsProFeatureEnabled(ProductFeatures.CsvExport))
        {
            await ShowProUpgradeDialogAsync("L’export CSV de l’historique");
            return;
        }

        var samples = _premiumHistory.GetLatest(GetHistoryRange());
        if (samples.Count == 0)
        {
            await ShowMessageDialogAsync("Aucun relevé à exporter", "L’historique local n’a pas encore de relevé pour cette période. Laisse la surveillance active puis réessaie.");
            return;
        }

        try
        {
            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("Fichier CSV", new List<string> { ".csv" });
            picker.SuggestedFileName = $"PCMonitorPro-{DateTime.Now:yyyyMMdd-HHmm}.csv";
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            var csv = new StringBuilder("timestamp,cpu_percent,memory_percent,download_bytes_per_second,upload_bytes_per_second,disk_read_bytes_per_second,disk_write_bytes_per_second,gpu_load_percent,gpu_memory_used_bytes,gpu_memory_total_bytes,gpu_temperature_c,cpu_temperature_c,fan_speed_rpm,network_interfaces\r\n");
            foreach (var sample in samples)
            {
                var adapters = sample.NetworkInterfaces is null
                    ? ""
                    : string.Join(" | ", sample.NetworkInterfaces.Select(network =>
                        $"{network.Name}: down {network.DownloadBytesPerSecond.ToString("0.##", CultureInfo.InvariantCulture)} B/s, up {network.UploadBytesPerSecond.ToString("0.##", CultureInfo.InvariantCulture)} B/s"));
                csv.Append(sample.CapturedAt.ToLocalTime().ToString("O", CultureInfo.InvariantCulture)).Append(',')
                    .Append(CsvNumber(sample.CpuUsagePercent)).Append(',')
                    .Append(CsvNumber(sample.MemoryUsagePercent)).Append(',')
                    .Append(sample.DownloadBytesPerSecond.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                    .Append(sample.UploadBytesPerSecond.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                    .Append(CsvNumber(sample.DiskReadBytesPerSecond)).Append(',')
                    .Append(CsvNumber(sample.DiskWriteBytesPerSecond)).Append(',')
                    .Append(CsvNumber(sample.GpuLoadPercent)).Append(',')
                    .Append(sample.GpuMemoryUsedBytes?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',')
                    .Append(sample.GpuMemoryTotalBytes?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',')
                    .Append(CsvNumber(sample.GpuTemperatureCelsius)).Append(',')
                    .Append(CsvNumber(sample.CpuTemperatureCelsius)).Append(',')
                    .Append(CsvNumber(sample.FanSpeedRpm)).Append(',')
                    .Append(CsvEscape(adapters)).Append("\r\n");
            }

            await FileIO.WriteTextAsync(file, csv.ToString());
            await ShowMessageDialogAsync("Export terminé", $"{samples.Count:N0} relevés exportés dans {file.Name}.");
        }
        catch (Exception exception)
        {
            await ShowMessageDialogAsync("Export impossible", $"Le fichier CSV n’a pas pu être enregistré. {exception.Message}");
        }
    }

    private static string CsvNumber(double? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";

    private static string CsvEscape(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private async Task GeneratePerformanceReportAsync()
    {
        if (!IsProFeatureEnabled(ProductFeatures.PerformanceReports))
        {
            await ShowProUpgradeDialogAsync("Le rapport de performances imprimable");
            return;
        }

        var range = GetHistoryRange();
        var samples = _premiumHistory.GetLatest(range);
        if (samples.Count == 0)
        {
            await ShowMessageDialogAsync("Aucun relevé pour ce rapport", "L’historique local ne contient pas encore de mesures pour cette période. Laisse la surveillance active puis réessaie.");
            return;
        }

        try
        {
            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("Rapport HTML", new List<string> { ".html" });
            picker.SuggestedFileName = $"PCMonitorPro-rapport-{DateTime.Now:yyyyMMdd-HHmm}.html";
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            var report = BuildPerformanceReportHtml(samples, range);
            await FileIO.WriteTextAsync(file, report);
            await ShowMessageDialogAsync("Rapport créé", $"Le rapport est enregistré dans {file.Name}. Ouvre-le dans un navigateur pour le consulter ou l’imprimer en PDF.");
        }
        catch (Exception exception)
        {
            await ShowMessageDialogAsync("Création du rapport impossible", $"Le rapport n’a pas pu être enregistré. {exception.Message}");
        }
    }

    private static string BuildPerformanceReportHtml(IReadOnlyList<MetricSample> samples, TimeSpan range)
    {
        var first = samples.Min(sample => sample.CapturedAt).ToLocalTime();
        var last = samples.Max(sample => sample.CapturedAt).ToLocalTime();
        var generated = DateTime.Now;
        var rows = new (string Label, string Value)[]
        {
            ("Processeur", SummarizeMetric(samples, sample => sample.CpuUsagePercent, "%")),
            ("Mémoire", SummarizeMetric(samples, sample => sample.MemoryUsagePercent, "%")),
            ("Réception réseau", SummarizeMetric(samples, sample => sample.DownloadBytesPerSecond, " Ko/s", 1024)),
            ("Envoi réseau", SummarizeMetric(samples, sample => sample.UploadBytesPerSecond, " Ko/s", 1024)),
            ("Lecture disque", SummarizeMetric(samples, sample => sample.DiskReadBytesPerSecond, " Ko/s", 1024)),
            ("Écriture disque", SummarizeMetric(samples, sample => sample.DiskWriteBytesPerSecond, " Ko/s", 1024)),
            ("Charge GPU", SummarizeMetric(samples, sample => sample.GpuLoadPercent, "%")),
            ("Mémoire vidéo utilisée", SummarizeMetric(samples, sample => ToGigabytes(sample.GpuMemoryUsedBytes), " Go")),
            ("Mémoire vidéo totale", SummarizeMetric(samples, sample => ToGigabytes(sample.GpuMemoryTotalBytes), " Go")),
            ("Température CPU", SummarizeMetric(samples, sample => sample.CpuTemperatureCelsius, " °C")),
            ("Température GPU", SummarizeMetric(samples, sample => sample.GpuTemperatureCelsius, " °C")),
            ("Vitesse du ventilateur", SummarizeMetric(samples, sample => sample.FanSpeedRpm, " tr/min"))
        };

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>")
            .AppendLine("<html lang=\"fr\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .AppendLine("<title>Rapport PC Monitor Pro</title>")
            .AppendLine("<style>body{font:16px/1.5 Segoe UI,Arial,sans-serif;color:#182230;background:#f3f6fa;margin:0;padding:32px}.sheet{max-width:920px;margin:auto;background:white;padding:36px;border-radius:18px;box-shadow:0 8px 32px #18304a18}h1{margin:0;color:#123b53}h2{margin-top:32px;color:#185d68}.muted{color:#607080}.facts{display:flex;gap:14px;flex-wrap:wrap;margin:20px 0}.fact{background:#f2f7f8;padding:14px 18px;border-radius:10px;min-width:150px}.fact b{display:block;color:#123b53}table{width:100%;border-collapse:collapse;margin-top:12px}th,td{text-align:left;padding:12px;border-bottom:1px solid #e6ebef}th{width:30%;color:#33495a}svg{width:100%;height:auto;background:#fbfcfd;border:1px solid #e6ebef;border-radius:12px}.legend{display:flex;gap:22px;margin:10px 0}.dot{display:inline-block;width:10px;height:10px;border-radius:50%;margin-right:6px}@media print{body{background:white;padding:0}.sheet{box-shadow:none;max-width:none;padding:0}.no-print{display:none}}</style>")
            .AppendLine("</head><body><main class=\"sheet\">")
            .AppendLine("<h1>Rapport de performances</h1><p class=\"muted\">PC Monitor Pro Premium · données locales de cet appareil</p>")
            .Append("<div class=\"facts\"><div class=\"fact\"><span>Période demandée</span><b>").Append(WebUtility.HtmlEncode(FormatHistoryRange(range))).Append("</b></div>")
            .Append("<div class=\"fact\"><span>Relevés utilisés</span><b>").Append(samples.Count.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))).Append("</b></div>")
            .Append("<div class=\"fact\"><span>Du</span><b>").Append(WebUtility.HtmlEncode(first.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.GetCultureInfo("fr-FR")))).Append("</b></div>")
            .Append("<div class=\"fact\"><span>Au</span><b>").Append(WebUtility.HtmlEncode(last.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.GetCultureInfo("fr-FR")))).Append("</b></div></div>")
            .AppendLine("<h2>Résumé des mesures</h2><p class=\"muted\">Les valeurs indiquent le minimum, la moyenne et le maximum des relevés collectés.</p><table><tbody>");

        foreach (var row in rows)
        {
            html.Append("<tr><th scope=\"row\">").Append(WebUtility.HtmlEncode(row.Label)).Append("</th><td>")
                .Append(WebUtility.HtmlEncode(row.Value)).AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table><h2>Évolution CPU et mémoire</h2>")
            .AppendLine("<div class=\"legend\"><span><i class=\"dot\" style=\"background:#129b82\"></i>CPU</span><span><i class=\"dot\" style=\"background:#527ee8\"></i>Mémoire</span></div>")
            .Append(BuildReportTrendSvg(samples))
            .Append("<p class=\"muted\">Rapport créé le ").Append(WebUtility.HtmlEncode(generated.ToString("dd/MM/yyyy à HH:mm:ss", CultureInfo.GetCultureInfo("fr-FR")))).AppendLine(". Les capteurs matériels peuvent être absents selon le PC et ses pilotes. Les données affichées sont issues de l’historique local et ne sont pas transmises au développeur.</p>")
            .AppendLine("<p class=\"no-print muted\">Pour obtenir un PDF, utilise la fonction Imprimer de ton navigateur et choisis « Enregistrer au format PDF ».</p>")
            .AppendLine("</main></body></html>");

        return html.ToString();
    }

    private static string SummarizeMetric(IEnumerable<MetricSample> samples, Func<MetricSample, double?> selector, string unit, double divisor = 1)
    {
        var values = samples.Select(selector)
            .Where(value => value is double number && double.IsFinite(number))
            .Select(value => value!.Value / divisor)
            .ToArray();
        if (values.Length == 0)
        {
            return "Capteur non disponible";
        }

        var culture = CultureInfo.GetCultureInfo("fr-FR");
        return $"Min. {values.Min().ToString("0.##", culture)}{unit} · moy. {values.Average().ToString("0.##", culture)}{unit} · max. {values.Max().ToString("0.##", culture)}{unit} · {values.Length:N0} relevés";
    }

    private static double? ToGigabytes(ulong? bytes) => bytes is ulong value ? value / 1_073_741_824d : null;

    private static string BuildReportTrendSvg(IReadOnlyList<MetricSample> samples)
    {
        const double width = 760;
        const double height = 230;
        const double left = 42;
        const double right = 18;
        const double top = 14;
        const double bottom = 30;
        var chartHeight = height - top - bottom;
        var chartWidth = width - left - right;
        var svg = new StringBuilder($"<svg role=\"img\" aria-label=\"Évolution du processeur et de la mémoire\" viewBox=\"0 0 {width:0} {height:0}\" xmlns=\"http://www.w3.org/2000/svg\">");
        for (var tick = 0; tick <= 100; tick += 25)
        {
            var y = top + chartHeight * (1 - tick / 100d);
            svg.Append("<line x1=\"").Append(left).Append("\" y1=\"").Append(y.ToString("0.##", CultureInfo.InvariantCulture))
                .Append("\" x2=\"").Append(width - right).Append("\" y2=\"").Append(y.ToString("0.##", CultureInfo.InvariantCulture))
                .AppendLine("\" stroke=\"#e4eaf0\"/><text x=\"8\" y=\"")
                .Append((y + 4).ToString("0.##", CultureInfo.InvariantCulture)).Append("\" fill=\"#657586\" font-size=\"12\">")
                .Append(tick).AppendLine("%</text>");
        }

        AppendTrendLine(svg, samples, sample => sample.CpuUsagePercent, "#129b82", left, top, chartWidth, chartHeight);
        AppendTrendLine(svg, samples, sample => sample.MemoryUsagePercent, "#527ee8", left, top, chartWidth, chartHeight);
        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static void AppendTrendLine(StringBuilder svg, IReadOnlyList<MetricSample> samples, Func<MetricSample, double?> selector, string color, double left, double top, double chartWidth, double chartHeight)
    {
        if (samples.Count == 0)
        {
            return;
        }

        var points = new List<string>();
        for (var index = 0; index < samples.Count; index++)
        {
            var value = selector(samples[index]);
            if (value is not double number || !double.IsFinite(number))
            {
                continue;
            }

            var x = left + (samples.Count == 1 ? chartWidth / 2 : chartWidth * index / (samples.Count - 1));
            var y = top + chartHeight * (1 - Math.Clamp(number, 0, 100) / 100d);
            points.Add($"{x.ToString("0.##", CultureInfo.InvariantCulture)},{y.ToString("0.##", CultureInfo.InvariantCulture)}");
        }

        if (points.Count > 0)
        {
            svg.Append("<polyline fill=\"none\" stroke=\"").Append(color).Append("\" stroke-width=\"3\" stroke-linecap=\"round\" stroke-linejoin=\"round\" points=\"")
                .Append(string.Join(" ", points)).AppendLine("\"/>");
        }
    }

    private static string FormatHistoryRange(TimeSpan range) => range.TotalHours >= 24
        ? "24 dernières heures"
        : range.TotalHours >= 6 ? "6 dernières heures" : "Dernière heure";

    private void OnHistoryCanvasSizeChanged(object sender, SizeChangedEventArgs args) => UpdateHistoryView();

    private void OnCpuChartSizeChanged(object sender, SizeChangedEventArgs args) => DrawCpuChart();

    private async void OnRefreshNowClicked(object sender, RoutedEventArgs args) => await RefreshMetricsAsync();

    private void OnPauseClicked(object sender, RoutedEventArgs args)
    {
        _isPaused = !_isPaused;
        PauseButton.Content = _isPaused ? "Reprendre" : "Pause";
        StatusText.Text = _isPaused ? "Surveillance en pause" : "Surveillance active";
        if (!_isPaused)
        {
            _ = RefreshMetricsAsync();
        }
    }

    private TaskbarIcon CreateTrayIcon()
    {
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem
        {
            Text = "Ouvrir PC Monitor Pro Premium",
            Command = new DelegateCommand(ShowWindow)
        });
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(new MenuFlyoutItem
        {
            Text = "Quitter",
            Command = new DelegateCommand(CloseApplication)
        });

        return new TaskbarIcon
        {
            Width = 0,
            Height = 0,
            Visibility = Visibility.Collapsed,
            ToolTipText = "PC Monitor Pro Premium",
            IconSource = new GeneratedIconSource
            {
                Text = "PC",
                Foreground = new SolidColorBrush(Color.FromArgb(255, 16, 21, 30)),
                Background = new SolidColorBrush(Color.FromArgb(255, 99, 214, 189)),
                FontSize = 42,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            },
            ContextFlyout = menu,
            DoubleClickCommand = new DelegateCommand(ShowWindow)
        };
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_settings.MinimizeToNotificationArea &&
            args.DidPresenterChange &&
            sender.Presenter is OverlappedPresenter presenter &&
            presenter.State == OverlappedPresenterState.Minimized)
        {
            WindowExtensions.Hide(this);
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_allowApplicationClose && _settings.MinimizeToNotificationArea)
        {
            args.Cancel = true;
            WindowExtensions.Hide(this);
        }
    }

    private void OnMinimizeToTrayToggled(object sender, RoutedEventArgs args)
    {
        if (_isUpdatingTrayPreference)
        {
            return;
        }

        var enabled = MinimizeToTrayToggle.IsOn;
        _settings.MinimizeToNotificationArea = enabled;
        SetTrayIconVisibility(enabled);
    }

    private async void OnStartWithWindowsToggled(object sender, RoutedEventArgs args)
    {
        if (_isUpdatingStartupPreference)
        {
            return;
        }

        var requestedState = StartWithWindowsToggle.IsOn;
        var result = await StartupManager.SetEnabledAsync(requestedState);
        _settings.StartWithWindows = result.Enabled;
        _isUpdatingStartupPreference = true;
        StartWithWindowsToggle.IsOn = result.Enabled;
        _isUpdatingStartupPreference = false;
        StartupStatusText.Text = result.Message;
    }

    private async Task SynchronizeStartupSettingAsync()
    {
        var currentState = await StartupManager.GetStateAsync();
        if (_settings.StartWithWindows && !currentState.Enabled)
        {
            currentState = await StartupManager.SetEnabledAsync(true);
            if (!currentState.Enabled)
            {
                _settings.StartWithWindows = false;
            }
        }

        _isUpdatingStartupPreference = true;
        StartWithWindowsToggle.IsOn = currentState.Enabled;
        _isUpdatingStartupPreference = false;
        StartupStatusText.Text = currentState.Message;
    }

    private void InitializeAlertControls()
    {
        var preferences = _settings.Alerts;
        var proEnabled = IsProFeatureEnabled(ProductFeatures.Alerts);
        _isResettingProToggle = true;
        AlertsEnabledToggle.IsOn = proEnabled && preferences.Enabled;
        CpuAlertToggle.IsOn = proEnabled && preferences.CpuEnabled;
        MemoryAlertToggle.IsOn = proEnabled && preferences.MemoryEnabled;
        DiskAlertToggle.IsOn = proEnabled && preferences.DiskEnabled;
        SelectThreshold(CpuThresholdComboBox, preferences.CpuThreshold);
        SelectThreshold(MemoryThresholdComboBox, preferences.MemoryThreshold);
        SelectThreshold(DiskThresholdComboBox, preferences.DiskThreshold);
        SelectTaggedItem(AlertDelayComboBox, preferences.TriggerDelaySeconds);
        SelectTaggedItem(AlertCooldownComboBox, preferences.CooldownMinutes);
        QuietHoursToggle.IsOn = proEnabled && preferences.QuietHoursEnabled;
        UpdateAlertControlsEnabledState();
        _isResettingProToggle = false;
    }

    private async void OnProFeatureToggled(object sender, RoutedEventArgs args)
    {
        if (_isResettingProToggle || sender is not ToggleSwitch toggle)
        {
            return;
        }

        if (!IsProFeatureEnabled(ProductFeatures.Alerts))
        {
            if (toggle.IsOn)
            {
                _isResettingProToggle = true;
                toggle.IsOn = false;
                _isResettingProToggle = false;
                await ShowProUpgradeDialogAsync("Les alertes personnalisées");
            }

            return;
        }

        var shouldRequestNotificationPermission = toggle == AlertsEnabledToggle && toggle.IsOn;
        if (toggle.IsOn && toggle != AlertsEnabledToggle && !AlertsEnabledToggle.IsOn)
        {
            _isResettingProToggle = true;
            AlertsEnabledToggle.IsOn = true;
            _isResettingProToggle = false;
            shouldRequestNotificationPermission = true;
        }

        UpdateAlertControlsEnabledState();
        SaveAlertPreferences();
        if (shouldRequestNotificationPermission)
        {
            await RequestNotificationAuthorizationAsync();
        }
    }

    private async void OnRequestNotificationsClicked(object sender, RoutedEventArgs args)
    {
        if (!IsProFeatureEnabled(ProductFeatures.Alerts))
        {
            await ShowProUpgradeDialogAsync("Les alertes personnalisées");
            return;
        }

        await RequestNotificationAuthorizationAsync();
    }

    private async Task RequestNotificationAuthorizationAsync()
    {
        var result = _alertNotifications.SendTestNotification();
        NotificationPermissionStatusText.Text = result.Message;

        var dialog = new ContentDialog
        {
            XamlRoot = WindowRoot.XamlRoot,
            Title = result.NeedsSettings ? "Autorisation Windows requise" : "Test des notifications",
            Content = result.Message,
            PrimaryButtonText = result.NeedsSettings ? "Ouvrir les paramètres" : "Compris",
            SecondaryButtonText = "Paramètres Windows",
            CloseButtonText = "Fermer",
            DefaultButton = result.NeedsSettings ? ContentDialogButton.Primary : ContentDialogButton.Close
        };

        var choice = await dialog.ShowAsync();
        if ((choice == ContentDialogResult.Primary && result.NeedsSettings) || choice == ContentDialogResult.Secondary)
        {
            OpenWindowsNotificationSettings();
        }
    }

    private void OpenWindowsNotificationSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:notifications") { UseShellExecute = true });
        }
        catch (Exception)
        {
            NotificationPermissionStatusText.Text = "Impossible d’ouvrir les paramètres. Va dans Paramètres Windows > Système > Notifications.";
        }
    }

    private void OnAlertThresholdChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_isResettingProToggle && IsProFeatureEnabled(ProductFeatures.Alerts))
        {
            SaveAlertPreferences();
        }
    }

    private async void OnAlertAdvancedPreferenceToggled(object sender, RoutedEventArgs args)
    {
        if (_isResettingProToggle || sender is not ToggleSwitch toggle)
        {
            return;
        }

        if (!IsProFeatureEnabled(ProductFeatures.Alerts))
        {
            _isResettingProToggle = true;
            toggle.IsOn = false;
            _isResettingProToggle = false;
            await ShowProUpgradeDialogAsync("Les réglages avancés des alertes");
            return;
        }

        SaveAlertPreferences();
    }

    private async void OnAlertAdvancedPreferenceSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_isResettingProToggle || sender is not ComboBox comboBox)
        {
            return;
        }

        if (!IsProFeatureEnabled(ProductFeatures.Alerts))
        {
            await ShowProUpgradeDialogAsync("Les réglages avancés des alertes");
            InitializeAlertControls();
            return;
        }

        SaveAlertPreferences();
    }

    private void SaveAlertPreferences()
    {
        _settings.Alerts = new AlertPreferences(
            AlertsEnabledToggle.IsOn,
            CpuAlertToggle.IsOn,
            GetSelectedThreshold(CpuThresholdComboBox, 85),
            MemoryAlertToggle.IsOn,
            GetSelectedThreshold(MemoryThresholdComboBox, 85),
            DiskAlertToggle.IsOn,
            GetSelectedThreshold(DiskThresholdComboBox, 90),
            GetSelectedTag(AlertDelayComboBox, 10),
            GetSelectedTag(AlertCooldownComboBox, 15),
            QuietHoursToggle.IsOn);
    }

    private void UpdateAlertControlsEnabledState()
    {
        var proEnabled = IsProFeatureEnabled(ProductFeatures.Alerts);
        var alertsEnabled = proEnabled && AlertsEnabledToggle.IsOn;
        CpuAlertToggle.IsEnabled = !proEnabled || alertsEnabled;
        MemoryAlertToggle.IsEnabled = !proEnabled || alertsEnabled;
        DiskAlertToggle.IsEnabled = !proEnabled || alertsEnabled;
        CpuThresholdComboBox.IsEnabled = alertsEnabled && CpuAlertToggle.IsOn;
        MemoryThresholdComboBox.IsEnabled = alertsEnabled && MemoryAlertToggle.IsOn;
        DiskThresholdComboBox.IsEnabled = alertsEnabled && DiskAlertToggle.IsOn;
        AlertDelayComboBox.IsEnabled = alertsEnabled;
        AlertCooldownComboBox.IsEnabled = alertsEnabled;
        QuietHoursToggle.IsEnabled = alertsEnabled;
    }

    private static void SelectThreshold(ComboBox comboBox, int threshold)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == threshold.ToString())
            ?? comboBox.Items.OfType<ComboBoxItem>().First();
    }

    private static void SelectTaggedItem(ComboBox comboBox, int value)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == value.ToString())
            ?? comboBox.Items.OfType<ComboBoxItem>().First();
    }

    private static int GetSelectedTag(ComboBox comboBox, int fallback) =>
        comboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var value)
            ? value
            : fallback;

    private static int GetSelectedThreshold(ComboBox comboBox, int fallback) =>
        comboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var threshold)
            ? threshold
            : fallback;

    private async void OnProFeatureClicked(object sender, RoutedEventArgs args)
    {
        var featureId = (sender as Button)?.Tag?.ToString();
        if (featureId is null || !IsProFeatureEnabled(featureId))
        {
            var title = featureId switch
            {
                ProductFeatures.PersistentHistory => "L’historique local",
                ProductFeatures.AdvancedCharts => "Les graphiques avancés",
                ProductFeatures.CsvExport => "L’export CSV",
                ProductFeatures.HardwareSensors => "Les capteurs matériels",
                ProductFeatures.ExtendedRefreshInterval => "Les intervalles étendus",
                ProductFeatures.PerformanceReports => "Les rapports de performances",
                _ => "Les alertes personnalisées"
            };
            await ShowProUpgradeDialogAsync(title);
            return;
        }

        switch (featureId)
        {
            case ProductFeatures.Alerts:
                RootNavigation.SelectedItem = RootNavigation.MenuItems.OfType<NavigationViewItem>().First(item => item.Tag?.ToString() == "alerts");
                break;
            case ProductFeatures.CsvExport:
                await ExportHistoryCsvAsync();
                break;
            case ProductFeatures.PerformanceReports:
                await GeneratePerformanceReportAsync();
                break;
            case ProductFeatures.ExtendedRefreshInterval:
                RootNavigation.SelectedItem = RootNavigation.MenuItems.OfType<NavigationViewItem>().First(item => item.Tag?.ToString() == "settings");
                break;
            default:
                RootNavigation.SelectedItem = RootNavigation.MenuItems.OfType<NavigationViewItem>().First(item => item.Tag?.ToString() == "history");
                break;
        }
    }

    private async void OnShowProOfferClicked(object sender, RoutedEventArgs args) =>
        await ShowProUpgradeDialogAsync("Les fonctions Premium marquées (PRO)");

    private async Task RefreshLicenseStateAsync()
    {
#if PCMONITORPRO_PREMIUM_TEST
        _licenseState = DevelopmentLicense.Current;
#else
        try
        {
            _licenseState = await _licenseProvider.GetCurrentLicenseAsync();
        }
        catch (Exception)
        {
            _licenseState = DevelopmentLicense.Current;
        }
#endif

        InitializeAlertControls();
        if (IsProFeatureEnabled(ProductFeatures.PersistentHistory))
        {
            await _premiumHistory.LoadAsync();
        }
        var savedInterval = _settings.RefreshIntervalSeconds;
        if (savedInterval > 10)
        {
            if (IsProFeatureEnabled(ProductFeatures.ExtendedRefreshInterval))
            {
                _timer.Interval = TimeSpan.FromSeconds(savedInterval);
                SelectRefreshInterval(savedInterval);
            }
            else
            {
                _settings.RefreshIntervalSeconds = 2;
                _timer.Interval = TimeSpan.FromSeconds(2);
                SelectRefreshInterval(2);
            }
        }
        UpdateLicenseDependentUi();
        UpdateHistoryView();
    }

    private void UpdateLicenseDependentUi()
    {
        PremiumOfferBanner.Visibility = IsProFeatureEnabled(ProductFeatures.Alerts)
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (IsProFeatureEnabled(ProductFeatures.Alerts))
        {
            _alertNotifications.NotificationInvoked -= OnAlertNotificationInvoked;
            _alertNotifications.NotificationInvoked += OnAlertNotificationInvoked;
            var notificationsAvailable = _alertNotifications.Register();
#if PCMONITORPRO_PREMIUM_TEST
            AlertsStatusText.Text = notificationsAvailable
                ? "MODE TEST : licence Premium simulée active. Aucun achat Microsoft Store n’a été effectué. Utilise « Autoriser et tester » pour vérifier les notifications."
                : "MODE TEST : licence Premium simulée active, mais Windows n’a pas enregistré les notifications. Utilise « Autoriser et tester » pour afficher les détails.";
#else
            AlertsStatusText.Text = notificationsAvailable
                ? "Licence Pro Microsoft Store active. Utilise « Autoriser et tester » pour vérifier les notifications Windows."
                : "Licence Pro active, mais Windows n’a pas enregistré les notifications. Utilise « Autoriser et tester » pour afficher les détails.";
#endif
        }
        else
        {
            _alertNotifications.Unregister();
            AlertsStatusText.Text = StoreCatalog.IsPackaged
                ? "Les alertes, l’historique 24 h, les graphiques, les exports, les rapports, les intervalles étendus et les capteurs matériels sont réservés à Premium (PRO). Active une option pour consulter l’offre Store."
                : "Les fonctions Premium (PRO) sont réservées à la version Microsoft Store. Depuis l’EXE, elles renvoient vers la fiche Store.";
        }
    }

    private async Task ShowProUpgradeDialogAsync(string featureName)
    {
        if (IsProFeatureEnabled(ProductFeatures.Alerts))
        {
            if (featureName.Contains("fonctions Premium", StringComparison.OrdinalIgnoreCase))
            {
#if PCMONITORPRO_PREMIUM_TEST
                await ShowMessageDialogAsync(
                    "Mode test Premium",
                    "La licence Premium simulée est active pour cet essai. Aucun achat Microsoft Store n’a été effectué. Les fonctions proposées sont déverrouillées dans ce build de test.");
#else
                await ShowMessageDialogAsync(
                    "PC Monitor Pro Premium activé",
                    "Les fonctions Premium sont déverrouillées sur cet appareil.");
#endif
                return;
            }

            var destination = featureName.Contains("historique", StringComparison.OrdinalIgnoreCase) ||
                              featureName.Contains("CSV", StringComparison.OrdinalIgnoreCase) ||
                              featureName.Contains("capteurs", StringComparison.OrdinalIgnoreCase) ||
                              featureName.Contains("rapport", StringComparison.OrdinalIgnoreCase) ||
                              featureName.Contains("graphiques", StringComparison.OrdinalIgnoreCase)
                ? "history"
                : featureName.Contains("intervalle", StringComparison.OrdinalIgnoreCase) ? "settings" : "alerts";
            RootNavigation.SelectedItem = RootNavigation.MenuItems
                .OfType<NavigationViewItem>()
                .First(item => item.Tag?.ToString() == destination);
#if PCMONITORPRO_PREMIUM_TEST
            await ShowMessageDialogAsync("Mode test Premium", $"Licence simulée active. Aucun achat Store n’a été effectué. {featureName} est disponible dans l’application.");
#else
            await ShowMessageDialogAsync("Licence Pro active", $"La licence Store est active. {featureName} est disponible dans l’application.");
#endif
            return;
        }

        var isPackaged = StoreCatalog.IsPackaged;
        var storeIdConfigured = isPackaged
            ? !string.IsNullOrWhiteSpace(StoreCatalog.ProAddOnStoreId)
            : !string.IsNullOrWhiteSpace(StoreCatalog.ListingProductId);
        var dialog = new ContentDialog
        {
            XamlRoot = WindowRoot.XamlRoot,
            Title = "Fonctionnalité réservée à la version PRO",
            Content = storeIdConfigured
                ? $"{featureName} sont réservées à PC Monitor Pro Premium. L’achat est unique; le Microsoft Store affichera le prix et demandera confirmation avant le paiement."
                : "L’offre PC Monitor Pro Premium n’est pas encore configurée dans le Microsoft Store.",
            PrimaryButtonText = storeIdConfigured
                ? isPackaged ? "Acheter dans le Microsoft Store" : "Ouvrir la version Store"
                : "Compris",
            CloseButtonText = "Plus tard",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary || !storeIdConfigured)
        {
            return;
        }

        if (!isPackaged)
        {
            OpenMicrosoftStoreListing();
            return;
        }

        var outcome = await _licenseProvider.PurchaseProAsync();
        if (outcome.Activated)
        {
            // Older test builds allowed saving alert preferences in the free tier. Require explicit opt-in after purchase.
            _settings.Alerts = new AlertPreferences(false, true, 85, true, 85, true, 90);
        }

        await RefreshLicenseStateAsync();
        await ShowMessageDialogAsync(outcome.Activated ? "PC Monitor Pro Premium activé" : "Achat Microsoft Store", outcome.Message);
    }

    private void OpenMicrosoftStoreListing()
    {
        var storeUri = new Uri($"ms-windows-store://pdp/?ProductId={StoreCatalog.ListingProductId}");
        try
        {
            Process.Start(new ProcessStartInfo(storeUri.ToString()) { UseShellExecute = true });
        }
        catch (Exception)
        {
            _ = ShowMessageDialogAsync("Microsoft Store indisponible", "Windows n’a pas pu ouvrir la fiche PC Monitor Pro Premium dans le Microsoft Store.");
        }
    }

    private async Task ShowMessageDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = WindowRoot.XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "Fermer",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
    }

    private bool IsProFeatureEnabled(string featureId) =>
        _featureGate.IsEnabled(featureId, _licenseState);

    private void OnThemeChoiceChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_isUpdatingThemeChoice || ThemeChoiceComboBox.SelectedItem is not ComboBoxItem item ||
            !Enum.TryParse<AppThemeChoice>(item.Tag?.ToString(), ignoreCase: true, out var choice))
        {
            return;
        }

        _settings.Theme = choice;
        ApplyTheme(choice);
    }

    private void ApplyTheme(AppThemeChoice choice)
    {
        WindowRoot.RequestedTheme = choice switch
        {
            AppThemeChoice.Light => ElementTheme.Light,
            AppThemeChoice.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void SelectThemeChoice(AppThemeChoice choice)
    {
        _isUpdatingThemeChoice = true;
        ThemeChoiceComboBox.SelectedItem = ThemeChoiceComboBox.Items
            .OfType<ComboBoxItem>()
            .First(item => item.Tag?.ToString() == choice.ToString());
        _isUpdatingThemeChoice = false;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        if (_lastSnapshot is not null)
        {
            UpdateDiskList(_lastSnapshot.Disks);
        }

        DrawCpuChart();
        UpdateHistoryView();
    }

    private SolidColorBrush GetThemeBrush(string key)
    {
        var dictionaryKey = WindowRoot.ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        if (Application.Current.Resources.ThemeDictionaries.TryGetValue(dictionaryKey, out var dictionaryValue) &&
            dictionaryValue is ResourceDictionary dictionary &&
            dictionary.TryGetValue(key, out var brushValue) &&
            brushValue is SolidColorBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    private void OnAlertNotificationInvoked()
    {
        _dispatcherQueue.TryEnqueue(ShowWindow);
    }

    private void SetTrayIconVisibility(bool visible)
    {
        _trayIcon.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowWindow()
    {
        WindowExtensions.Show(this);
        Activate();
    }

    private void CloseApplication()
    {
        _allowApplicationClose = true;
        Close();
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _timer.Stop();
        _trayIcon.Dispose();
        _alertNotifications.Unregister();
        if (IsProFeatureEnabled(ProductFeatures.PersistentHistory))
        {
            await _premiumHistory.FlushAsync();
        }

        if (_metricsCollector is IDisposable disposableCollector)
        {
            disposableCollector.Dispose();
        }
    }

    private async void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag?.ToString();
        if (tag == "history" && !IsProFeatureEnabled(ProductFeatures.PersistentHistory))
        {
            sender.SelectedItem = sender.MenuItems.OfType<NavigationViewItem>().First(item => item.Tag?.ToString() == "dashboard");
            await ShowProUpgradeDialogAsync("L’historique local et les graphiques Premium");
            return;
        }

        DashboardView.Visibility = tag == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        AlertsView.Visibility = tag == "alerts" ? Visibility.Visible : Visibility.Collapsed;
        HistoryView.Visibility = tag == "history" ? Visibility.Visible : Visibility.Collapsed;
        AboutView.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;
        ProView.Visibility = tag == "pro" ? Visibility.Visible : Visibility.Collapsed;
        if (tag == "history")
        {
            UpdateHistoryView();
        }
    }

    private async void OnRefreshIntervalChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_isUpdatingInterval || SettingsRefreshIntervalComboBox.SelectedItem is not ComboBoxItem item ||
            !int.TryParse(item.Tag?.ToString(), out var seconds))
        {
            return;
        }

        if (seconds > 10 && !IsProFeatureEnabled(ProductFeatures.ExtendedRefreshInterval))
        {
            var fallback = _settings.RefreshIntervalSeconds <= 10 ? _settings.RefreshIntervalSeconds : 2;
            if (_settings.RefreshIntervalSeconds > 10)
            {
                _settings.RefreshIntervalSeconds = fallback;
            }
            SelectRefreshInterval(fallback);
            await ShowProUpgradeDialogAsync("Les intervalles de mise à jour de 15, 30 et 60 secondes");
            return;
        }

        _settings.RefreshIntervalSeconds = seconds;
        _timer.Interval = TimeSpan.FromSeconds(seconds);
        _ = RefreshMetricsAsync();
    }

    private void SelectRefreshInterval(int seconds)
    {
        _isUpdatingInterval = true;
        SettingsRefreshIntervalComboBox.SelectedItem = SettingsRefreshIntervalComboBox.Items
            .OfType<ComboBoxItem>()
            .First(item => item.Tag?.ToString() == seconds.ToString());
        _isUpdatingInterval = false;
    }

    private static string FormatBytes(ulong bytes)
    {
        string[] units = ["o", "Ko", "Mo", "Go", "To"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }

    private static string FormatBytesPerSecond(double bytesPerSecond) => FormatBytes((ulong)Math.Max(0, bytesPerSecond));

    private static string FormatDuration(int seconds) => seconds < 60
        ? $"{seconds} s"
        : $"{Math.Max(1, seconds / 60)} min";

    private sealed class DelegateCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
