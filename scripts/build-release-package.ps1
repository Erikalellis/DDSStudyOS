param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$SelfContained = "true",
    [string]$WindowsAppSDKSelfContained = "true",
    [string]$InstallWebView2 = "true",
    [string]$InstallDotNetDesktopRuntime = "false",
    [int]$DotNetDesktopRuntimeMajor = 8,
    [string]$OutputPath = "artifacts\installer-output",
    [string]$StableSetupBaseName = "DDSStudyOS-Setup",
    [string]$BetaSetupBaseName = "DDSStudyOS-Beta-Setup",
    [string]$PortableBaseName = "DDSStudyOS-Portable",
    [string]$ShaFileName = "DDSStudyOS-SHA256.txt",
    [string]$BetaVersion = "",
    [switch]$SkipBeta,
    [switch]$SkipPortable,
    [switch]$SkipChangelogCheck,
    [switch]$SignArtifacts,
    [switch]$SignAppExecutable,
    [string]$CertThumbprint = "6780CE530A33615B591727F5334B3DD075B76422",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [ValidateSet("CurrentUser", "LocalMachine")]
    [string]$CertStoreScope = "CurrentUser",
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

function Resolve-ProductVersion {
    param([string]$RepoRoot)

    $projectFile = Join-Path $RepoRoot "src\DDSStudyOS.App\DDSStudyOS.App.csproj"
    if (-not (Test-Path $projectFile)) {
        throw "Projeto nao encontrado para resolver versao: $projectFile"
    }

    [xml]$xml = Get-Content $projectFile -Raw -Encoding UTF8
    $rawVersion = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($rawVersion)) {
        throw "Tag <Version> nao encontrada em: $projectFile"
    }

    return ($rawVersion -split '[\+\-]')[0]
}

function Assert-VersionInChangelog {
    param(
        [string]$ChangelogPath,
        [string]$Version
    )

    if (-not (Test-Path $ChangelogPath)) {
        throw "CHANGELOG nao encontrado: $ChangelogPath"
    }

    $content = Get-Content $ChangelogPath -Raw -Encoding UTF8
    if ($content -notmatch [regex]::Escape($Version)) {
        throw "Versao $Version nao encontrada no CHANGELOG. Atualize o changelog antes de gerar release."
    }
}

function Update-UpdateInfo {
    param(
        [string]$FilePath,
        [string]$Version,
        [string]$InstallerAssetName,
        [string]$UpdatedAtUtc
    )

    if (-not (Test-Path $FilePath)) {
        throw "Arquivo update-info nao encontrado: $FilePath"
    }

    $json = Get-Content $FilePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $json.currentVersion = $Version
    $json.installerAssetName = $InstallerAssetName
    $json.updatedAtUtc = $UpdatedAtUtc

    $json | ConvertTo-Json -Depth 10 | Set-Content -Path $FilePath -Encoding UTF8
}

function Invoke-InnoBuild {
    param(
        [string]$BuildScript,
        [string]$Configuration,
        [string]$Platform,
        [string]$RuntimeIdentifier,
        [string]$SelfContained,
        [string]$WindowsAppSDKSelfContained,
        [string]$OutputPath,
        [string]$SetupBaseName,
        [string]$InstallWebView2,
        [string]$InstallDotNetDesktopRuntime,
        [int]$DotNetDesktopRuntimeMajor,
        [string]$PrepareInput,
        [switch]$SignInstaller,
        [switch]$SignExecutable,
        [string]$CertThumbprint,
        [string]$PfxPath,
        [string]$PfxPassword,
        [string]$CertStoreScope,
        [string]$TimestampUrl
    )

    $args = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $BuildScript,
        "-Configuration", $Configuration,
        "-Platform", $Platform,
        "-RuntimeIdentifier", $RuntimeIdentifier,
        "-SelfContained", $SelfContained,
        "-WindowsAppSDKSelfContained", $WindowsAppSDKSelfContained,
        "-OutputPath", $OutputPath,
        "-SetupBaseName", $SetupBaseName,
        "-InstallWebView2", $InstallWebView2,
        "-InstallDotNetDesktopRuntime", $InstallDotNetDesktopRuntime,
        "-DotNetDesktopRuntimeMajor", $DotNetDesktopRuntimeMajor,
        "-PrepareInput", $PrepareInput
    )

    if ($SignInstaller) {
        $args += "-SignInstaller"
    }

    if ($SignExecutable) {
        $args += "-SignExecutable"
    }

    if (-not [string]::IsNullOrWhiteSpace($CertThumbprint)) {
        $args += @("-CertThumbprint", $CertThumbprint)
    }

    if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
        $args += @("-PfxPath", $PfxPath, "-PfxPassword", $PfxPassword)
    }

    if (-not [string]::IsNullOrWhiteSpace($CertStoreScope)) {
        $args += @("-CertStoreScope", $CertStoreScope)
    }

    if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
        $args += @("-TimestampUrl", $TimestampUrl)
    }

    powershell @args
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao gerar instalador: $SetupBaseName"
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
$buildInnoScript = Join-Path $PSScriptRoot "build-inno-installer.ps1"

if (-not (Test-Path $buildInnoScript)) {
    throw "Script nao encontrado: $buildInnoScript"
}

if (-not (Test-Path $resolvedOutputPath)) {
    New-Item -ItemType Directory -Path $resolvedOutputPath -Force | Out-Null
}

$stableVersion = Resolve-ProductVersion -RepoRoot $repoRoot
$effectiveBetaVersion = if ([string]::IsNullOrWhiteSpace($BetaVersion)) { "$stableVersion-beta.1" } else { $BetaVersion }

$changelogPath = Join-Path $repoRoot "CHANGELOG.md"
if (-not $SkipChangelogCheck) {
    Assert-VersionInChangelog -ChangelogPath $changelogPath -Version $stableVersion
}

Write-Host "==> Release package"
Write-Host "Stable version: $stableVersion"
Write-Host "Beta version: $effectiveBetaVersion"
Write-Host "Output: $resolvedOutputPath"

Write-Host ""
Write-Host "==> Gerando setup estavel"
Invoke-InnoBuild `
    -BuildScript $buildInnoScript `
    -Configuration $Configuration `
    -Platform $Platform `
    -RuntimeIdentifier $RuntimeIdentifier `
    -SelfContained $SelfContained `
    -WindowsAppSDKSelfContained $WindowsAppSDKSelfContained `
    -OutputPath $OutputPath `
    -SetupBaseName $StableSetupBaseName `
    -InstallWebView2 $InstallWebView2 `
    -InstallDotNetDesktopRuntime $InstallDotNetDesktopRuntime `
    -DotNetDesktopRuntimeMajor $DotNetDesktopRuntimeMajor `
    -PrepareInput "1" `
    -SignInstaller:$SignArtifacts `
    -SignExecutable:$SignAppExecutable `
    -CertThumbprint $CertThumbprint `
    -PfxPath $PfxPath `
    -PfxPassword $PfxPassword `
    -CertStoreScope $CertStoreScope `
    -TimestampUrl $TimestampUrl

$stableSetupPath = Join-Path $resolvedOutputPath "$StableSetupBaseName.exe"
if (-not (Test-Path $stableSetupPath)) {
    throw "Setup estavel nao encontrado: $stableSetupPath"
}

$betaSetupPath = $null
if (-not $SkipBeta) {
    Write-Host ""
    Write-Host "==> Gerando setup beta"
    Invoke-InnoBuild `
        -BuildScript $buildInnoScript `
        -Configuration $Configuration `
        -Platform $Platform `
        -RuntimeIdentifier $RuntimeIdentifier `
        -SelfContained $SelfContained `
        -WindowsAppSDKSelfContained $WindowsAppSDKSelfContained `
        -OutputPath $OutputPath `
        -SetupBaseName $BetaSetupBaseName `
        -InstallWebView2 $InstallWebView2 `
        -InstallDotNetDesktopRuntime $InstallDotNetDesktopRuntime `
        -DotNetDesktopRuntimeMajor $DotNetDesktopRuntimeMajor `
        -PrepareInput "0" `
        -SignInstaller:$SignArtifacts `
        -SignExecutable:$false `
        -CertThumbprint $CertThumbprint `
        -PfxPath $PfxPath `
        -PfxPassword $PfxPassword `
        -CertStoreScope $CertStoreScope `
        -TimestampUrl $TimestampUrl

    $betaSetupPath = Join-Path $resolvedOutputPath "$BetaSetupBaseName.exe"
    if (-not (Test-Path $betaSetupPath)) {
        throw "Setup beta nao encontrado: $betaSetupPath"
    }
}

$portableZipPath = $null
if (-not $SkipPortable) {
    $installerInputAppPath = Join-Path $repoRoot "artifacts\installer-input\app"
    if (-not (Test-Path $installerInputAppPath)) {
        throw "Pasta para gerar portatil nao encontrada: $installerInputAppPath"
    }

    $portableZipPath = Join-Path $resolvedOutputPath "$PortableBaseName.zip"
    if (Test-Path $portableZipPath) {
        Remove-Item $portableZipPath -Force
    }

    Write-Host ""
    Write-Host "==> Gerando pacote portatil"
    Compress-Archive -Path (Join-Path $installerInputAppPath "*") -DestinationPath $portableZipPath -CompressionLevel Optimal -Force
}

$updatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
$stableUpdateInfo = Join-Path $repoRoot "installer\update\stable\update-info.json"
$betaUpdateInfo = Join-Path $repoRoot "installer\update\beta\update-info.json"

Update-UpdateInfo -FilePath $stableUpdateInfo -Version $stableVersion -InstallerAssetName "$StableSetupBaseName.exe" -UpdatedAtUtc $updatedAtUtc
if (-not $SkipBeta) {
    Update-UpdateInfo -FilePath $betaUpdateInfo -Version $effectiveBetaVersion -InstallerAssetName "$BetaSetupBaseName.exe" -UpdatedAtUtc $updatedAtUtc
}

$shaPath = Join-Path $resolvedOutputPath $ShaFileName
$artifacts = @($stableSetupPath)
if ($betaSetupPath) {
    $artifacts += $betaSetupPath
}
if ($portableZipPath) {
    $artifacts += $portableZipPath
}

$shaLines = foreach ($artifact in $artifacts) {
    $hash = (Get-FileHash -Path $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash *$([System.IO.Path]::GetFileName($artifact))"
}

$shaLines | Set-Content -Path $shaPath -Encoding UTF8

Write-Host ""
Write-Host "Release package concluido."
Write-Host "Artefatos:"
Write-Host "- $stableSetupPath"
if ($betaSetupPath) { Write-Host "- $betaSetupPath" }
if ($portableZipPath) { Write-Host "- $portableZipPath" }
Write-Host "- $shaPath"
Write-Host "Update info sincronizado:"
Write-Host "- $stableUpdateInfo ($stableVersion)"
if (-not $SkipBeta) { Write-Host "- $betaUpdateInfo ($effectiveBetaVersion)" }
