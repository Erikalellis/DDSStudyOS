param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputDirectory = "",
    [string]$SelfContained = "true",
    [string]$WindowsAppSDKSelfContained = "true",
    [switch]$SignExecutable,
    [string]$CertThumbprint = "6780CE530A33615B591727F5334B3DD075B76422",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [ValidateSet("CurrentUser", "LocalMachine")]
    [string]$CertStoreScope = "CurrentUser",
    [string]$TimestampUrl = ""
)

$ErrorActionPreference = "Stop"

function ConvertTo-BoolValue {
    param(
        [string]$Value,
        [string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Parametro '$Name' nao pode ser vazio."
    }

    switch ($Value.Trim().ToLowerInvariant()) {
        "1" { return $true }
        "true" { return $true }
        "yes" { return $true }
        "y" { return $true }
        "0" { return $false }
        "false" { return $false }
        "no" { return $false }
        "n" { return $false }
        default { throw "Valor invalido para '$Name': $Value. Use true/false ou 1/0." }
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\installer-input"
}

$publishScript = Join-Path $PSScriptRoot "build-release.ps1"
$signScript = Join-Path $PSScriptRoot "sign-release.ps1"
$publishDir = Join-Path $repoRoot "src\DDSStudyOS.App\bin\$Platform\$Configuration\net8.0-windows10.0.19041.0\$RuntimeIdentifier\publish"
$exePath = Join-Path $publishDir "DDSStudyOS.App.exe"

if (-not (Test-Path $publishScript)) {
    throw "Script de build nao encontrado: $publishScript"
}

Write-Host "==> Build/Publish"
$selfContainedValue = ConvertTo-BoolValue -Value $SelfContained -Name "SelfContained"
$windowsAppSdkSelfContainedValue = ConvertTo-BoolValue -Value $WindowsAppSDKSelfContained -Name "WindowsAppSDKSelfContained"
$selfContainedArg = if ($selfContainedValue) { "1" } else { "0" }
$windowsAppSdkSelfContainedArg = if ($windowsAppSdkSelfContainedValue) { "1" } else { "0" }
powershell -NoProfile -ExecutionPolicy Bypass -File $publishScript -Configuration $Configuration -Platform $Platform -RuntimeIdentifier $RuntimeIdentifier -SelfContained $selfContainedArg -WindowsAppSDKSelfContained $windowsAppSdkSelfContainedArg
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
    $signArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $signScript,
        "-TargetPaths", $exePath
    )

    if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
        $signArgs += @("-TimestampUrl", $TimestampUrl)
    }

    if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
        $signArgs += @("-PfxPath", $PfxPath, "-PfxPassword", $PfxPassword)
    }
    else {
        $signArgs += @("-CertThumbprint", $CertThumbprint, "-CertStoreScope", $CertStoreScope)
    }

    powershell @signArgs
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

# Copia os arquivos do publish para o input do instalador.
# Importante: nunca copie pastas de cache/estado geradas em runtime (ex.: WebView2 user data),
# pois podem estar mudando enquanto copiamos e quebrar o build do instalador.
$excludedRootDirs = @(
    "DDS_StudyOS" # Pasta que pode aparecer quando o WebView2 grava dados ao lado do executável
)

$items = Get-ChildItem -Path $publishDir -Force
foreach ($item in $items) {
    if ($item.PSIsContainer) {
        if ($excludedRootDirs -contains $item.Name) {
            Write-Host "Ignorando pasta de runtime/cache no publish: $($item.Name)"
            continue
        }

        # Pastas do tipo "DDSStudyOS.App.exe.WebView2" (user data) não fazem parte do app.
        if ($item.Name.EndsWith(".WebView2", [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Host "Ignorando pasta de runtime/cache do WebView2 no publish: $($item.Name)"
            continue
        }
    }

    Copy-Item -Path $item.FullName -Destination $appOut -Recurse -Force
}
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
    Platform = $Platform
    RuntimeIdentifier = $RuntimeIdentifier
    SelfContained = [bool]$selfContainedValue
    WindowsAppSDKSelfContained = [bool]$windowsAppSdkSelfContainedValue
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

