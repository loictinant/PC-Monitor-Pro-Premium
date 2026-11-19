#requires -RunAsAdministrator

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "src\PCMonitor.App\PCMonitor.App.csproj"
$projectDefinition = [xml](Get-Content -LiteralPath $projectPath -Raw)
$projectVersion = $projectDefinition.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1 -ExpandProperty Version
$packageVersion = $projectDefinition.Project.PropertyGroup | Where-Object { $_.AppxPackageVersion } | Select-Object -First 1 -ExpandProperty AppxPackageVersion
if ([string]::IsNullOrWhiteSpace($projectVersion) -or [string]::IsNullOrWhiteSpace($packageVersion)) {
    throw "Version de l’application introuvable dans $projectPath"
}
$testPackageRoot = Join-Path $repositoryRoot "artifacts\PCMonitorPro-msix-v$projectVersion-local-test"
$packagePath = Join-Path $testPackageRoot "PCMonitor.App_${packageVersion}_x64.msix"
$reportPath = Join-Path $repositoryRoot "artifacts\PCMonitorPro-WACK-v$projectVersion.xml"
$programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")
$appCertPath = Join-Path $programFilesX86 "Windows Kits\10\App Certification Kit\appcert.exe"

if (-not (Test-Path -LiteralPath $packagePath)) {
    throw "Paquet signé de test introuvable : $packagePath. Lance d'abord scripts\Sign-Test-Msix.ps1."
}

if (-not (Test-Path -LiteralPath $appCertPath)) {
    throw "Windows App Certification Kit introuvable. Installe le kit depuis le SDK Windows, puis relance ce script."
}

& $appCertPath reset
if ($LASTEXITCODE -ne 0) {
    throw "La réinitialisation de WACK a échoué (code $LASTEXITCODE)."
}

& $appCertPath test -appxpackagepath $packagePath -reportoutputpath $reportPath
if ($LASTEXITCODE -ne 0) {
    throw "WACK a terminé avec le code $LASTEXITCODE. Consulte le rapport : $reportPath"
}

Write-Host "Rapport WACK : $reportPath"
Write-Host "Ouvre le rapport et vérifie chaque test, même si le processus retourne le code 0."
