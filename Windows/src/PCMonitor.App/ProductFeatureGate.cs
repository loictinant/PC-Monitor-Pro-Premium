using ProductLicensing.Abstractions;

namespace PCMonitor.App;

internal static class ProductFeatures
{
    public const string Alerts = "alerts";
    public const string PersistentHistory = "persistent-history";
    public const string CsvExport = "csv-export";
    public const string AdvancedCharts = "advanced-charts";
    public const string HardwareSensors = "hardware-sensors";
}

/// <summary>
/// Enables only Pro features that are implemented and covered by a valid license.
/// </summary>
internal sealed class ProductFeatureGate : IFeatureGate
{
    // Only alerts are implemented in this build. Add a feature here when its behavior ships.
    private static readonly HashSet<string> ImplementedProFeatures =
    [
        ProductFeatures.Alerts
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
    public static LicenseState Current { get; } = new(
        "PCMonitorPro",
        LicenseTier.Free,
        LicenseSource.None,
        DateTimeOffset.UtcNow);
}
