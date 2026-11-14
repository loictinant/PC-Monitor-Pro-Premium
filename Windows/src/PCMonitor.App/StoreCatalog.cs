using System.Reflection;
using Windows.ApplicationModel;

namespace PCMonitor.App;

internal static class StoreCatalog
{
    public static string ListingProductId { get; } = GetMetadata("PCMonitorPro.StoreListingProductId");
    public static string ProAddOnStoreId { get; } = GetMetadata("PCMonitorPro.ProAddOnStoreId");
    public static string ProAddOnProductId { get; } = GetMetadata("PCMonitorPro.ProAddOnProductId");

    public static bool IsPackaged
    {
        get
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

    private static string GetMetadata(string key) =>
        typeof(StoreCatalog).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)?.Value
        ?? string.Empty;
}
