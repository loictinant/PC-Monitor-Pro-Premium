param(
    [ValidateSet("x64")]
    [string]$Runtime = "x64",
    [string]$StoreListingProductId = "9P34JVNCWKJJ",
    [string]$ProAddOnStoreId = ""
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "src\PCMonitor.App\PCMonitor.App.csproj"
$nugetConfigPath = Join-Path $repositoryRoot "NuGet.Config"
$projectDefinition = [xml](Get-Content -LiteralPath $projectPath -Raw)
$projectVersion = $projectDefinition.Project.PropertyGroup |
    Where-Object { $_.Version } |
    Select-Object -First 1 -ExpandProperty Version
if ([string]::IsNullOrWhiteSpace($projectVersion)) {
    throw "Version de l’application introuvable dans $projectPath"
}
$outputPath = Join-Path $repositoryRoot "artifacts\PCMonitorPro-exe-$Runtime-v$projectVersion"
$runtimeIdentifier = "win-$Runtime"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 10 SDK introuvable. Installe .NET 10 SDK et les outils Windows App SDK/MSIX, puis relance ce script."
}

# Windows App SDK requires extraction for side-by-side DLL redirection in a single-file build.
dotnet publish $projectPath `
    --configuration Release `
    --runtime $runtimeIdentifier `
    --output $outputPath `
    --configfile $nugetConfigPath `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -p:SelfContained=true `
    -p:EnableMsixTooling=true `
    "-p:StoreListingProductId=$StoreListingProductId" `
    "-p:ProAddOnStoreId=$ProAddOnStoreId" `
    -p:IncludeAllContentForSelfExtract=true `
    -p:PublishSingleFile=true

if ($LASTEXITCODE -ne 0) {
    throw "La publication de l’EXE a échoué (code $LASTEXITCODE). Consulte les erreurs affichées ci-dessus."
}

$exePath = Join-Path $outputPath "PCMonitorPro.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "La publication est terminée, mais l’EXE attendu est absent : $exePath"
}

Write-Host "EXE de test : $exePath"
