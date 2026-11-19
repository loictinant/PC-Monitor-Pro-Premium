$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "src\PCMonitor.App\PCMonitor.App.csproj"
$projectDefinition = [xml](Get-Content -LiteralPath $projectPath -Raw)
$projectVersion = $projectDefinition.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1 -ExpandProperty Version
$packageVersion = $projectDefinition.Project.PropertyGroup | Where-Object { $_.AppxPackageVersion } | Select-Object -First 1 -ExpandProperty AppxPackageVersion
if ([string]::IsNullOrWhiteSpace($projectVersion) -or [string]::IsNullOrWhiteSpace($packageVersion)) {
    throw "Version de l’application introuvable dans $projectPath"
}
$packageRoot = Join-Path $repositoryRoot "artifacts\PCMonitorPro-msix-v$projectVersion-local-test"
$packagePath = Join-Path $packageRoot "PCMonitor.App_${packageVersion}_x64.msix"
$certificatePath = Join-Path $packageRoot "PCMonitorPro-LocalTest.cer"
$dependencyPath = Join-Path $packageRoot "Dependencies\x64\Microsoft.WindowsAppRuntime.2.msix"
$expectedSubject = "CN=D67E21A8-DC00-45EF-86D1-375158FB659F"

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
    throw "Certificat non approuvé sur cet ordinateur. Lance d'abord scripts\Trust-Test-Msix-Certificate.ps1 dans PowerShell administrateur, puis réessaie dans ta session Windows normale."
}

Add-AppxPackage -Path $packagePath -DependencyPath $dependencyPath -ForceUpdateFromAnyVersion -ErrorAction Stop
$installedPackage = Get-AppxPackage -Name "TINANTLOIC.PCMonitorProPremium" -ErrorAction Stop

Write-Host "Installation MSIX de test terminée : version $($installedPackage.Version)."
Write-Host "Ouvre PC Monitor Pro Premium depuis le menu Démarrer."
