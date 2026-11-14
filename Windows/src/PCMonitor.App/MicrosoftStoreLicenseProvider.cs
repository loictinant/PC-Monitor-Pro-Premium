using Microsoft.UI.Xaml;
using ProductLicensing.Abstractions;
using Windows.Services.Store;

namespace PCMonitor.App;

/// <summary>
/// Reads the lifetime Pro add-on entitlement and requests purchases only from the Store-packaged app.
/// Store IDs are supplied as build metadata after Partner Center setup.
/// </summary>
internal sealed class MicrosoftStoreLicenseProvider : ILicenseProvider
{
    private readonly Window _ownerWindow;
    private readonly string _proAddOnStoreId;
    private readonly string _proAddOnProductId;
    private StoreContext? _storeContext;

    public MicrosoftStoreLicenseProvider(Window ownerWindow, string proAddOnStoreId, string proAddOnProductId)
    {
        _ownerWindow = ownerWindow;
        _proAddOnStoreId = proAddOnStoreId.Trim();
        _proAddOnProductId = proAddOnProductId.Trim();
    }

    public async Task<LicenseState> GetCurrentLicenseAsync(CancellationToken cancellationToken = default)
    {
        if (!StoreCatalog.IsPackaged ||
            string.IsNullOrWhiteSpace(_proAddOnStoreId) ||
            string.IsNullOrWhiteSpace(_proAddOnProductId) ||
            cancellationToken.IsCancellationRequested)
        {
            return FreeLicense(LicenseSource.None);
        }

        try
        {
            var appLicense = await GetStoreContext().GetAppLicenseAsync();
            var hasPro = appLicense.AddOnLicenses.Values.Any(addOnLicense =>
                string.Equals(addOnLicense.InAppOfferToken, _proAddOnProductId, StringComparison.OrdinalIgnoreCase));

            return new LicenseState(
                "PCMonitorPro",
                hasPro ? LicenseTier.Pro : LicenseTier.Free,
                LicenseSource.MicrosoftStore,
                DateTimeOffset.UtcNow);
        }
        catch (Exception)
        {
            // Store errors, offline startup, or an unassociated development package always fall back to Free.
            return FreeLicense(LicenseSource.None);
        }
    }

    public async Task<StorePurchaseOutcome> PurchaseProAsync()
    {
        if (!StoreCatalog.IsPackaged)
        {
            return new StorePurchaseOutcome(false, "Installe PC Monitor Pro Premium depuis le Microsoft Store pour activer Premium.");
        }

        if (string.IsNullOrWhiteSpace(_proAddOnStoreId))
        {
            return new StorePurchaseOutcome(false, "L’achat Pro n’est pas encore configuré. L’identifiant du module Pro du Microsoft Store manque.");
        }

        try
        {
            var result = await GetStoreContext().RequestPurchaseAsync(_proAddOnStoreId);
            if (result.Status is StorePurchaseStatus.Succeeded or StorePurchaseStatus.AlreadyPurchased)
            {
                var currentLicense = await GetCurrentLicenseAsync();
                return currentLicense.Tier == LicenseTier.Pro
                    ? new StorePurchaseOutcome(true, "La licence Premium a été restaurée ou activée.")
                    : new StorePurchaseOutcome(false, "Le Store a répondu, mais la licence Pro n’est pas encore visible. Réessaie de vérifier la licence.");
            }

            var detail = result.ExtendedError is null ? string.Empty : $" Détail : {result.ExtendedError.Message}";
            return new StorePurchaseOutcome(false, result.Status switch
            {
                StorePurchaseStatus.NotPurchased => "L’achat n’a pas été finalisé." + detail,
                StorePurchaseStatus.NetworkError => "Le Microsoft Store n’a pas pu être joint." + detail,
                StorePurchaseStatus.ServerError => "Le Microsoft Store a rencontré une erreur." + detail,
                _ => "Le Microsoft Store n’a pas confirmé l’achat." + detail
            });
        }
        catch (Exception exception)
        {
            return new StorePurchaseOutcome(false, $"La demande d’achat Store a échoué. {exception.Message}");
        }
    }

    private StoreContext GetStoreContext()
    {
        if (_storeContext is not null)
        {
            return _storeContext;
        }

        var context = StoreContext.GetDefault();
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_ownerWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(context, windowHandle);
        _storeContext = context;
        return context;
    }

    private static LicenseState FreeLicense(LicenseSource source) => new(
        "PCMonitorPro",
        LicenseTier.Free,
        source,
        DateTimeOffset.UtcNow);
}

internal sealed record StorePurchaseOutcome(bool Activated, string Message);
