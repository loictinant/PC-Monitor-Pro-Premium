namespace ProductLicensing.Abstractions;

public enum LicenseTier
{
    Free,
    Pro
}

public enum LicenseSource
{
    None,
    MicrosoftStore,
    ExternalLicense
}

public sealed record LicenseState(
    string ProductId,
    LicenseTier Tier,
    LicenseSource Source,
    DateTimeOffset CheckedAt,
    DateTimeOffset? ExpiresAt = null);

public interface ILicenseProvider
{
    Task<LicenseState> GetCurrentLicenseAsync(CancellationToken cancellationToken = default);
}

public interface IFeatureGate
{
    bool IsEnabled(string featureId, LicenseState license);
}
