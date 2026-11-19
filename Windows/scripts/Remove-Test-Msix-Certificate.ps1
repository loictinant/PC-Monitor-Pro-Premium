#requires -RunAsAdministrator

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "src\PCMonitor.App\PCMonitor.App.csproj"
$projectDefinition = [xml](Get-Content -LiteralPath $projectPath -Raw)
$projectVersion = $projectDefinition.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1 -ExpandProperty Version
if ([string]::IsNullOrWhiteSpace($projectVersion)) {
    throw "Version de l’application introuvable dans $projectPath"
}
$certificatePath = Join-Path $repositoryRoot "artifacts\PCMonitorPro-msix-v$projectVersion-local-test\PCMonitorPro-LocalTest.cer"

if (-not (Test-Path -LiteralPath $certificatePath)) {
    throw "Certificat de test introuvable : $certificatePath"
}

$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
$trustedPath = "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)"
if (Test-Path -LiteralPath $trustedPath) {
    Remove-Item -LiteralPath $trustedPath
    Write-Host "Certificat de test retiré de LocalMachine\TrustedPeople."
}
else {
    Write-Host "Le certificat n'était pas présent dans LocalMachine\TrustedPeople."
}

Write-Host "La clé privée de développement reste dans CurrentUser\My pour les prochains essais."
