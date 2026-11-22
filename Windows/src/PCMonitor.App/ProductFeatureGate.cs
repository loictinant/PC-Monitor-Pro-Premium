using ProductLicensing.Abstractions;

namespace PCMonitor.App;

internal static class ProductFeatures
{
    public const string Alerts = "alerts";
    public const string PersistentHistory = "persistent-history";
    public const string CsvExport = "csv-export";
    public const string AdvancedCharts = "advanced-charts";
    public const string HardwareSensors = "hardware-sensors";
    public const string ExtendedRefreshInterval = "extended-refresh-interval";
    public const string PerformanceReports = "performance-reports";
}

/// <summary>
/// Enables only Pro features that are implemented and covered by a valid license.
/// </summary>
internal sealed class ProductFeatureGate : IFeatureGate
{
    // Features are released only after their UI, local behavior, and licensing check are implemented.
    private static readonly HashSet<string> ImplementedProFeatures =
    [
        ProductFeatures.Alerts,
        ProductFeatures.PersistentHistory,
        ProductFeatures.CsvExport,
        ProductFeatures.AdvancedCharts,
        ProductFeatures.HardwareSensors,
        ProductFeatures.ExtendedRefreshInterval,
        ProductFeatures.PerformanceReports
    ];

    public bool IsEnabled(string featureId, LicenseState license) =>
        license.ProductId == "PCMonitorPro" &&
        license.Tier == LicenseTier.Pro &&
        license.Source is LicenseSource.MicrosoftStore or LicenseSource.ExternalLicense &&
        (license.ExpiresAt is null || license.ExpiresAt > DateTimeOffset.UtcNow) &&
        ImplementedProFeatures.Contains(featureId);
}

internal static class DevelopmentLicense
{
#if PCMONITORPRO_PREMIUM_TEST
    public static LicenseState Current { get; } = new(
        "PCMonitorPro",
        LicenseTier.Pro,
        LicenseSource.ExternalLicense,
        CheckedAt: DateTimeOffset.UtcNow,
        ExpiresAt: null);
#else
    public static LicenseState Current { get; } = new(
        "PCMonitorPro",
        LicenseTier.Free,
        LicenseSource.None,
        DateTimeOffset.UtcNow);
#endif
}
