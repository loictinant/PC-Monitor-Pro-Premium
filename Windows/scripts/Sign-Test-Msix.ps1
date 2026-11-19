$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "src\PCMonitor.App\PCMonitor.App.csproj"
$projectDefinition = [xml](Get-Content -LiteralPath $projectPath -Raw)
$projectVersion = $projectDefinition.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1 -ExpandProperty Version
$packageVersion = $projectDefinition.Project.PropertyGroup | Where-Object { $_.AppxPackageVersion } | Select-Object -First 1 -ExpandProperty AppxPackageVersion
if ([string]::IsNullOrWhiteSpace($projectVersion) -or [string]::IsNullOrWhiteSpace($packageVersion)) {
    throw "Version de l’application introuvable dans $projectPath"
}
$storePackageRoot = Join-Path $repositoryRoot "artifacts\PCMonitorPro-msix-upload-v$projectVersion"
$testPackageRoot = Join-Path $repositoryRoot "artifacts\PCMonitorPro-msix-v$projectVersion-local-test"
$testPackageFolder = "PCMonitor.App_${packageVersion}_x64_Test"
$packageFileName = "PCMonitor.App_${packageVersion}_x64.msix"
$sourcePackage = Join-Path $storePackageRoot "$testPackageFolder\$packageFileName"
$testPackage = Join-Path $testPackageRoot $packageFileName
$sourceDependency = Join-Path $storePackageRoot "$testPackageFolder\Dependencies\x64\Microsoft.WindowsAppRuntime.2.msix"
$testDependency = Join-Path $testPackageRoot "Dependencies\x64\Microsoft.WindowsAppRuntime.2.msix"
$certificatePath = Join-Path $testPackageRoot "PCMonitorPro-LocalTest.cer"
$expectedName = "TINANTLOIC.PCMonitorProPremium"
$expectedPublisher = "CN=D67E21A8-DC00-45EF-86D1-375158FB659F"

foreach ($path in @($sourcePackage, $sourceDependency)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Fichier requis introuvable : $path. Génère d'abord le paquet avec scripts\Build-Msix.ps1."
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($sourcePackage)
try {
    $manifestEntry = $archive.GetEntry("AppxManifest.xml")
    if (-not $manifestEntry) {
        throw "Le manifeste AppxManifest.xml est absent du paquet source."
    }

    $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
    try {
        [xml]$manifest = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
}
finally {
    $archive.Dispose()
}

if ($manifest.Package.Identity.Name -ne $expectedName -or $manifest.Package.Identity.Publisher -ne $expectedPublisher) {
    throw "Identité du MSIX inattendue. Aucun paquet ne sera signé. Vérifie Name et Publisher dans le manifeste."
}

$signTool = Get-Command signtool.exe -ErrorAction SilentlyContinue
if ($signTool) {
    $signToolPath = $signTool.Source
}
else {
    $sdkPackageRoots = @(
        (Join-Path $env:USERPROFILE ".nuget\packages\microsoft.windows.sdk.buildtools"),
        "C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages\microsoft.windows.sdk.buildtools"
    )
    $signToolCandidates = foreach ($sdkPackageRoot in $sdkPackageRoots) {
        if (Test-Path -LiteralPath $sdkPackageRoot) {
            Get-ChildItem -LiteralPath $sdkPackageRoot -Filter "signtool.exe" -File -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match "\\bin\\[^\\]+\\x64\\signtool\.exe$" }
        }
    }
    $signToolPath = ($signToolCandidates | Sort-Object FullName -Descending | Select-Object -First 1).FullName
}

if (-not $signToolPath -or -not (Test-Path -LiteralPath $signToolPath)) {
    throw "SignTool.exe introuvable. Installe les outils Windows SDK puis relance le script."
}

$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $expectedPublisher -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $expectedPublisher `
        -FriendlyName "PC Monitor Pro local test only" `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddMonths(12)
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $testDependency) | Out-Null
Copy-Item -LiteralPath $sourcePackage -Destination $testPackage -Force
Copy-Item -LiteralPath $sourceDependency -Destination $testDependency -Force
Export-Certificate -Cert $certificate -FilePath $certificatePath -Force | Out-Null

& $signToolPath sign /fd SHA256 /sha1 $certificate.Thumbprint $testPackage
if ($LASTEXITCODE -ne 0) {
    throw "La signature locale du paquet a échoué (code $LASTEXITCODE)."
}

$packageSignature = Get-AuthenticodeSignature -FilePath $testPackage
if (-not $packageSignature.SignerCertificate -or $packageSignature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    throw "Le MSIX ne porte pas la signature du certificat de test attendu."
}
if ($packageSignature.Status -notin @("Valid", "UnknownError")) {
    throw "La vérification de l'intégrité de la signature a échoué : $($packageSignature.Status) — $($packageSignature.StatusMessage)"
}

$readmePath = Join-Path $testPackageRoot "LISEZ-MOI.txt"
@"
Paquet de test local PC Monitor Pro Premium $packageVersion (x64)

Ce MSIX est signé avec un certificat auto-signé de développement. Il sert uniquement aux essais locaux et ne doit pas être envoyé au Microsoft Store ni distribué.
Le paquet d’envoi Store non signé reste séparé dans artifacts\PCMonitorPro-msix-upload-v$projectVersion.

Publisher : $expectedPublisher
Certificat public : PCMonitorPro-LocalTest.cer
Empreinte : $($certificate.Thumbprint)
Expiration : $($certificate.NotAfter.ToString("yyyy-MM-dd"))

Le script de signature n'ajoute le certificat à aucun magasin de confiance. Le script d'installation l'importera dans LocalMachine\TrustedPeople après avoir été lancé en PowerShell administrateur. La clé privée n'est pas exportée; elle reste dans le magasin CurrentUser\My.
"@ | Set-Content -LiteralPath $readmePath -Encoding utf8

Write-Host "Paquet de test signé créé : $testPackage"
Write-Host "Certificat public : $certificatePath"
Write-Host "Ce paquet est réservé aux essais locaux; ne pas le soumettre au Store."
