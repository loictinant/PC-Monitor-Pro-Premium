param(
    [string]$StoreListingProductId = "9P34JVNCWKJJ",
    [string]$ProAddOnStoreId = "9MTVFWCM41XS",
    [string]$ProAddOnProductId = "PCMONITORPRO_PREMIUM_LIFETIME"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "src\PCMonitor.App\PCMonitor.App.csproj"
$nugetConfigPath = Join-Path $repositoryRoot "NuGet.Config"
$outputPath = Join-Path $repositoryRoot "artifacts\PCMonitorPro-msix"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 10 SDK introuvable. Installe .NET 10 SDK et les outils Windows App SDK/MSIX, puis relance ce script."
}

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
dotnet publish $projectPath `
    --configuration Release `
    --output $outputPath `
    --configfile $nugetConfigPath `
    -p:Platform=x64 `
    "-p:StoreListingProductId=$StoreListingProductId" `
    "-p:ProAddOnStoreId=$ProAddOnStoreId" `
    "-p:ProAddOnProductId=$ProAddOnProductId" `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageDir="$outputPath\" `
    -p:AppxPackageSigningEnabled=false

if ($LASTEXITCODE -ne 0) {
    throw "La génération MSIX a échoué (code $LASTEXITCODE). Consulte les erreurs affichées ci-dessus."
}

$msixPackages = @(Get-ChildItem -LiteralPath $outputPath -Filter "*.msix" -Recurse)
if ($msixPackages.Count -eq 0) {
    throw "La génération s’est terminée, mais aucun fichier MSIX n’a été trouvé dans : $outputPath"
}

Write-Host "Paquet MSIX non signé généré sous : $outputPath"
Write-Host "Remplace l'identité provisoire et configure la signature avant toute distribution."
