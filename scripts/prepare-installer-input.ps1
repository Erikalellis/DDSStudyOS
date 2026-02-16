param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win10-x64",
    [string]$OutputDirectory = "",
    [switch]$SignExecutable,
    [string]$CertThumbprint = "6780CE530A33615B591727F5334B3DD075B76422"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\installer-input"
}

$publishScript = Join-Path $PSScriptRoot "build-release.ps1"
$signScript = Join-Path $PSScriptRoot "sign-release.ps1"
$publishDir = Join-Path $repoRoot "src\DDSStudyOS.App\bin\$Configuration\net8.0-windows10.0.19041.0\$RuntimeIdentifier\publish"
$exePath = Join-Path $publishDir "DDSStudyOS.App.exe"

if (-not (Test-Path $publishScript)) {
    throw "Script de build nao encontrado: $publishScript"
}

Write-Host "==> Build/Publish"
powershell -NoProfile -ExecutionPolicy Bypass -File $publishScript -Configuration $Configuration -RuntimeIdentifier $RuntimeIdentifier
if ($LASTEXITCODE -ne 0) {
    throw "Falha no build/publish."
}

if (-not (Test-Path $exePath)) {
    throw "Executavel publicado nao encontrado: $exePath"
}

if ($SignExecutable) {
    if (-not (Test-Path $signScript)) {
        throw "Script de assinatura nao encontrado: $signScript"
    }
    Write-Host "==> Assinando executavel"
    powershell -NoProfile -ExecutionPolicy Bypass -File $signScript -CertThumbprint $CertThumbprint -TargetPaths @($exePath)
    if ($LASTEXITCODE -ne 0) {
        throw "Falha na assinatura do executavel."
    }
}

Write-Host "==> Preparando pasta do instalador em: $OutputDirectory"
if (Test-Path $OutputDirectory) {
    Remove-Item $OutputDirectory -Recurse -Force
}

$appOut = Join-Path $OutputDirectory "app"
$scriptsOut = Join-Path $OutputDirectory "scripts"
$docsOut = Join-Path $OutputDirectory "docs"
$legalOut = Join-Path $OutputDirectory "legal"
New-Item -ItemType Directory -Path $appOut, $scriptsOut, $docsOut, $legalOut -Force | Out-Null

Copy-Item (Join-Path $publishDir "*") $appOut -Recurse -Force
Copy-Item (Join-Path $repoRoot "scripts\install-internal-cert.ps1") $scriptsOut -Force
Copy-Item (Join-Path $repoRoot "scripts\Instalar_DDS.bat") $scriptsOut -Force
Copy-Item (Join-Path $repoRoot "scripts\DDS_Studios_Final.cer") $scriptsOut -Force

Copy-Item (Join-Path $repoRoot "SUPPORT.md") $docsOut -Force
Copy-Item (Join-Path $repoRoot "CHANGELOG.md") $docsOut -Force
Copy-Item (Join-Path $repoRoot "docs\UPDATE_INFO.md") $docsOut -Force
Copy-Item (Join-Path $repoRoot "docs\ADVANCED_INSTALLER_SETUP.md") $docsOut -Force

$legalSource = Join-Path $repoRoot "installer\legal"
if (Test-Path $legalSource) {
    Copy-Item (Join-Path $legalSource "*") $legalOut -Recurse -Force
}

$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
$manifest = [pscustomobject]@{
    Product = "DDS StudyOS"
    Company = "Deep Darkness Studios"
    Version = $versionInfo.ProductVersion
    BuildTimeUtc = (Get-Date).ToUniversalTime().ToString("o")
    RuntimeIdentifier = $RuntimeIdentifier
    Signed = [bool]$SignExecutable
    MainExecutable = "app\\DDSStudyOS.App.exe"
    EulaPtBrPath = "legal\\EULA.pt-BR.rtf"
    EulaEsPath = "legal\\EULA.es.rtf"
    InstallerReadmePath = "legal\\README_INSTALLER.pt-BR.rtf"
    SupportUrl = "https://github.com/Erikalellis/DDSStudyOS/blob/main/SUPPORT.md"
    UpdateInfoUrl = "https://github.com/Erikalellis/DDSStudyOS/blob/main/docs/UPDATE_INFO.md"
    ReleaseNotesUrl = "https://github.com/Erikalellis/DDSStudyOS/blob/main/CHANGELOG.md"
}

$manifestPath = Join-Path $OutputDirectory "installer-manifest.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -Encoding UTF8

Write-Host ""
Write-Host "Pacote para instalador gerado com sucesso."
Write-Host "Pasta: $OutputDirectory"
Write-Host "Executavel: $exePath"
Write-Host "Manifesto: $manifestPath"
