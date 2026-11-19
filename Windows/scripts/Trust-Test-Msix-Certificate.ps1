#requires -RunAsAdministrator

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "src\PCMonitor.App\PCMonitor.App.csproj"
$projectDefinition = [xml](Get-Content -LiteralPath $projectPath -Raw)
$projectVersion = $projectDefinition.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1 -ExpandProperty Version
if ([string]::IsNullOrWhiteSpace($projectVersion)) {
    throw "Version de l’application introuvable dans $projectPath"
}
$packageRoot = Join-Path $repositoryRoot "artifacts\PCMonitorPro-msix-v$projectVersion-local-test"
$certificatePath = Join-Path $packageRoot "PCMonitorPro-LocalTest.cer"
$expectedSubject = "CN=D67E21A8-DC00-45EF-86D1-375158FB659F"

if (-not (Test-Path -LiteralPath $certificatePath)) {
    throw "Certificat de test introuvable : $certificatePath. Lance d'abord scripts\Sign-Test-Msix.ps1."
}

$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
if ($certificate.Subject -ne $expectedSubject) {
    throw "Le sujet du certificat ne correspond pas au Publisher MSIX attendu. Aucun changement n'a été effectué."
}

$trustedCertificate = Get-ChildItem Cert:\LocalMachine\TrustedPeople |
    Where-Object Thumbprint -eq $certificate.Thumbprint |
    Select-Object -First 1

if (-not $trustedCertificate) {
    Import-Certificate -FilePath $certificatePath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
}

Write-Host "Certificat de test approuvé pour les installations MSIX locales : $($certificate.Thumbprint)"
Write-Host "Ferme cette fenêtre administrateur, puis lance scripts\Install-Test-Msix.ps1 dans ta session Windows normale."
