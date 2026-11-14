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
using System.Windows.Input;
using Windows.UI;

namespace PCMonitor.App;

public sealed partial class MainWindow : Window
{
    private const int MaximumHistorySamples = 900;
    private const int VisibleChartSamples = 60;
    private readonly IMetricsCollector _metricsCollector = new WindowsMetricsCollector();
    private readonly MetricsHistory _history = new();
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
        Title = "PC Monitor Pro Premium";
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _licenseProvider = new MicrosoftStoreLicenseProvider(
            this,
            StoreCatalog.ProAddOnStoreId,
            StoreCatalog.ProAddOnProductId);
        ApplyTheme(_settings.Theme);
        SelectThemeChoice(_settings.Theme);
        WindowRoot.ActualThemeChanged += OnActualThemeChanged;
        LoadVersionHistory();

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
        _ = RefreshLicenseStateAsync();

        RootNavigation.SelectedItem = RootNavigation.MenuItems.OfType<NavigationViewItem>().First();
        SelectRefreshInterval(_settings.RefreshIntervalSeconds);

        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds);
        _timer.IsRepeating = true;
        _timer.Tick += OnTimerTick;
        _timer.Start();

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
            DiskDetailText.Text = $"{busiestDisk.Name} · {FormatBytes(busiestDisk.FreeBytes)} libres";
        }
        else
        {
            DiskValueText.Text = "—";
            DiskProgressBar.Value = 0;
            DiskDetailText.Text = "Aucun volume fixe prêt";
        }

        DownloadValueText.Text = $"{FormatBytesPerSecond(snapshot.DownloadBytesPerSecond)} /s";
        UploadValueText.Text = $"{FormatBytesPerSecond(snapshot.UploadBytesPerSecond)} /s";
        ChartCurrentText.Text = snapshot.CpuUsagePercent is double chartCpu ? $"{chartCpu:0}%" : "—";
        var chartSamples = _history.GetLatest(VisibleChartSamples);
        var chartSeconds = chartSamples.Count < 2
            ? 0
            : (int)(chartSamples[^1].CapturedAt - chartSamples[0].CapturedAt).TotalSeconds;
        ChartTimeRangeText.Text = chartSeconds == 0
            ? "Collecte de l’historique…"
            : $"Dernières {FormatDuration(chartSeconds)}";
        LastUpdatedText.Text = $"Dernier relevé : {snapshot.CapturedAt.ToLocalTime():HH:mm:ss} · intervalle de {_settings.RefreshIntervalSeconds} s";

        UpdateDiskList(snapshot.Disks);
        DrawCpuChart();
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

        var samples = _history.GetLatest(VisibleChartSamples)
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

        if (toggle.IsOn && toggle != AlertsEnabledToggle && !AlertsEnabledToggle.IsOn)
        {
            _isResettingProToggle = true;
            AlertsEnabledToggle.IsOn = true;
            _isResettingProToggle = false;
        }

        UpdateAlertControlsEnabledState();
        SaveAlertPreferences();
    }

    private void OnAlertThresholdChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_isResettingProToggle && IsProFeatureEnabled(ProductFeatures.Alerts))
        {
            SaveAlertPreferences();
        }
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
            GetSelectedThreshold(DiskThresholdComboBox, 90));
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
    }

    private static void SelectThreshold(ComboBox comboBox, int threshold)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == threshold.ToString())
            ?? comboBox.Items.OfType<ComboBoxItem>().First();
    }

    private static int GetSelectedThreshold(ComboBox comboBox, int fallback) =>
        comboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var threshold)
            ? threshold
            : fallback;

    private async void OnProFeatureClicked(object sender, RoutedEventArgs args)
    {
        var featureId = (sender as Button)?.Tag?.ToString();
        if (featureId != ProductFeatures.Alerts)
        {
            await ShowMessageDialogAsync(
                "Fonction Pro à venir",
                "Cette fonction fait partie de l’offre Premium, mais elle n’est pas encore disponible. Elle ne sera déverrouillable qu’après son ajout dans une prochaine version.");
            return;
        }

        await ShowProUpgradeDialogAsync("Les alertes personnalisées");
    }

    private async void OnShowProOfferClicked(object sender, RoutedEventArgs args) =>
        await ShowProUpgradeDialogAsync("Les fonctions Premium marquées (PRO)");

    private async Task RefreshLicenseStateAsync()
    {
        try
        {
            _licenseState = await _licenseProvider.GetCurrentLicenseAsync();
        }
        catch (Exception)
        {
            _licenseState = DevelopmentLicense.Current;
        }

        InitializeAlertControls();
        UpdateLicenseDependentUi();
    }

    private void UpdateLicenseDependentUi()
    {
        if (IsProFeatureEnabled(ProductFeatures.Alerts))
        {
            _alertNotifications.NotificationInvoked -= OnAlertNotificationInvoked;
            _alertNotifications.NotificationInvoked += OnAlertNotificationInvoked;
            var notificationsAvailable = _alertNotifications.Register();
            AlertsStatusText.Text = notificationsAvailable
                ? "Licence Pro Microsoft Store active. Les alertes sont locales; vérifie que Windows autorise les notifications."
                : "Licence Pro active, mais les notifications Windows ne sont pas disponibles dans cette session.";
        }
        else
        {
            _alertNotifications.Unregister();
            AlertsStatusText.Text = StoreCatalog.IsPackaged
                ? "Les alertes sont réservées à la version payante Pro. Active une option pour acheter l’extension dans le Microsoft Store."
                : "Les alertes sont réservées à la version Pro du Microsoft Store. Depuis l’EXE, les options Pro renvoient vers la fiche Store.";
        }
    }

    private async Task ShowProUpgradeDialogAsync(string featureName)
    {
        if (IsProFeatureEnabled(ProductFeatures.Alerts))
        {
            if (featureName.Contains("fonctions Premium", StringComparison.OrdinalIgnoreCase))
            {
                await ShowMessageDialogAsync(
                    "PC Monitor Pro Premium activé",
                    "Les fonctions Premium déjà livrées sont déverrouillées. Les fonctions signalées « à venir » seront activées dans leur future version.");
                return;
            }

            RootNavigation.SelectedItem = RootNavigation.MenuItems
                .OfType<NavigationViewItem>()
                .First(item => item.Tag?.ToString() == "alerts");
            await ShowMessageDialogAsync("Licence Pro active", $"La licence Store est active. {featureName} peut être réglée dans la page Alertes Pro.");
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
                ? $"{featureName} sont réservées à PC Monitor Pro Premium, vendu uniquement dans le Microsoft Store."
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

    private void LoadVersionHistory()
    {
        VersionHistoryPanel.Children.Clear();
        VersionHistoryPanel.Children.Add(new TextBlock
        {
            Text = "Historique des versions",
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = GetThemeBrush("PrimaryTextBrush")
        });
        VersionHistoryPanel.Children.Add(new TextBlock
        {
            Text = "Journal complet des versions de PC Monitor Pro Premium.",
            FontSize = 13,
            Foreground = GetThemeBrush("MutedTextBrush"),
            Margin = new Thickness(0, -8, 0, 4)
        });

        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("PCMonitor.App.VersionHistory.md");
        if (stream is null)
        {
            VersionHistoryPanel.Children.Add(new TextBlock
            {
                Text = "Le fichier CHANGELOG.md n’est pas inclus dans cette version.",
                Foreground = GetThemeBrush("MutedTextBrush")
            });
            return;
        }

        using var reader = new StreamReader(stream);
        string? version = null;
        var changes = new List<string>();

        void AddVersionCard()
        {
            if (version is null)
            {
                return;
            }

            var content = new StackPanel { Spacing = 8 };
            content.Children.Add(new TextBlock
            {
                Text = version,
                FontSize = 17,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = GetThemeBrush("PrimaryTextBrush")
            });
            foreach (var change in changes)
            {
                content.Children.Add(new TextBlock
                {
                    Text = $"•  {change}",
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = GetThemeBrush("MutedTextBrush")
                });
            }

            VersionHistoryPanel.Children.Add(new Border
            {
                Background = GetThemeBrush("PanelBrush"),
                BorderBrush = GetThemeBrush("PanelBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Child = content
            });
            changes.Clear();
        }

        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                AddVersionCard();
                version = line[3..].Trim();
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) && version is not null)
            {
                changes.Add(line[2..].Trim());
            }
        }

        AddVersionCard();
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

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _trayIcon.Dispose();
        _alertNotifications.Unregister();
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag?.ToString();
        DashboardView.Visibility = tag == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        AlertsView.Visibility = tag == "alerts" ? Visibility.Visible : Visibility.Collapsed;
        HistoryView.Visibility = tag == "history" ? Visibility.Visible : Visibility.Collapsed;
        AboutView.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;
        ProView.Visibility = tag == "pro" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnRefreshIntervalChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_isUpdatingInterval || SettingsRefreshIntervalComboBox.SelectedItem is not ComboBoxItem item ||
            !int.TryParse(item.Tag?.ToString(), out var seconds))
        {
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
