#requires -RunAsAdministrator

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$packageRoot = Join-Path $repositoryRoot "artifacts\PCMonitorPro-msix"
$packagePath = Join-Path $packageRoot "PCMonitor.App_0.1.0.0_x64_Test\PCMonitor.App_0.1.0.0_x64.msix"
$certificatePath = Join-Path $packageRoot "PCMonitorPro-TestCert.cer"
$dependencyPath = Join-Path $packageRoot "PCMonitor.App_0.1.0.0_x64_Test\Dependencies\x64\Microsoft.WindowsAppRuntime.2.msix"
$expectedSubject = "CN=PC Monitor Pro Development"

foreach ($path in @($packagePath, $certificatePath, $dependencyPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Fichier requis introuvable : $path"
    }
}

$packageSignature = Get-AuthenticodeSignature -FilePath $packagePath
$testCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
if (-not $packageSignature.SignerCertificate -or
    $packageSignature.SignerCertificate.Thumbprint -ne $testCertificate.Thumbprint -or
    $testCertificate.Subject -ne $expectedSubject) {
    throw "Le certificat ne correspond pas au MSIX de test attendu. Aucun changement n'a été effectué."
}

$trustedCertificate = Get-ChildItem Cert:\LocalMachine\TrustedPeople |
    Where-Object Thumbprint -eq $testCertificate.Thumbprint |
    Select-Object -First 1

if (-not $trustedCertificate) {
    Import-Certificate -FilePath $certificatePath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
}

Add-AppxPackage -Path $packagePath -DependencyPath $dependencyPath -ForceUpdateFromAnyVersion -ErrorAction Stop
$installedPackage = Get-AppxPackage -Name "PCMonitorPro.Local" -ErrorAction Stop

Write-Host "Installation MSIX de test terminée : version $($installedPackage.Version)."
Write-Host "Ouvre PC Monitor Pro Premium depuis le menu Démarrer."
