param(
    [string]$StoreListingProductId = "9P34JVNCWKJJ",
    [string]$ProAddOnStoreId = "9MTVFWCM41XS",
    [string]$ProAddOnProductId = "PCMONITORPRO_PREMIUM_LIFETIME"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "PCMonitorPro.sln"
$projectPath = Join-Path $repositoryRoot "src\PCMonitor.App\PCMonitor.App.csproj"
$projectDefinition = [xml](Get-Content -LiteralPath $projectPath -Raw)
$projectVersion = $projectDefinition.Project.PropertyGroup |
    Where-Object { $_.Version } |
    Select-Object -First 1 -ExpandProperty Version
if ([string]::IsNullOrWhiteSpace($projectVersion)) {
    throw "Version de l’application introuvable dans $projectPath"
}
$outputPath = Join-Path $repositoryRoot "artifacts\PCMonitorPro-msix-upload-v$projectVersion"

$msbuildCommand = Get-Command msbuild.exe -ErrorAction SilentlyContinue
$msbuildPath = if ($msbuildCommand) { $msbuildCommand.Source } else { $null }

if (-not $msbuildPath) {
    $programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")
    $vswherePath = Join-Path $programFilesX86 "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswherePath) {
        $msbuildPath = & $vswherePath -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
            Select-Object -First 1
    }
}

if (-not $msbuildPath -or -not (Test-Path -LiteralPath $msbuildPath)) {
    throw "MSBuild de Visual Studio introuvable. Installe Visual Studio avec la charge Développement d’applications WinUI, puis relance ce script."
}

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
& $msbuildPath $solutionPath `
    /nologo `
    /m `
    /v:minimal `
    /p:Configuration=Release `
    /p:Platform=x64 `
    /p:AppxBundle=Never `
    /p:UapAppxPackageBuildMode=StoreUpload `
    "/p:AppxPackageDir=$outputPath\" `
    /p:AppxPackageSigningEnabled=false `
    /p:AppxSymbolPackageEnabled=false `
    /p:GenerateAppxPackageOnBuild=true `
    "/p:StoreListingProductId=$StoreListingProductId" `
    "/p:ProAddOnStoreId=$ProAddOnStoreId" `
    "/p:ProAddOnProductId=$ProAddOnProductId"

if ($LASTEXITCODE -ne 0) {
    throw "La génération MSIX a échoué (code $LASTEXITCODE). Consulte les erreurs affichées ci-dessus."
}

$uploadPackage = Get-ChildItem -LiteralPath $outputPath -Filter "*.msixupload" -File | Select-Object -First 1
if (-not $uploadPackage) {
    throw "La génération s’est terminée, mais aucun fichier .msixupload n’a été trouvé dans : $outputPath"
}

Write-Host "Paquet d’envoi Microsoft Store généré : $($uploadPackage.FullName)"
Write-Host "Architecture empaquetée : x64. Le projet ne génère pas encore de bundle multi-architecture."
Write-Host "Le fichier .msixupload n’est pas signé localement; le Microsoft Store le signe après certification."
Write-Host "Identité configurée : TINANTLOIC.PCMonitorProPremium / CN=D67E21A8-DC00-45EF-86D1-375158FB659F"
Write-Host "Ce paquet n’a pas été soumis au Microsoft Store. Vérifie le résultat et la certification avant toute distribution."
