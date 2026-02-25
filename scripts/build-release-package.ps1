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
    [string]$GitHubOwner = "",
    [string]$GitHubRepo = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
$repoLinksScript = Join-Path $PSScriptRoot "repo-links.ps1"
if (-not (Test-Path $repoLinksScript)) {
    throw "Script de links do repositorio nao encontrado: $repoLinksScript"
}
. $repoLinksScript

function Invoke-WithRetry {
    param(
        [scriptblock]$Action,
        [string]$Description,
        [int]$MaxAttempts = 20,
        [int]$DelayMilliseconds = 750
    )

    if ($MaxAttempts -lt 1) { $MaxAttempts = 1 }
    if ($DelayMilliseconds -lt 0) { $DelayMilliseconds = 0 }

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            return & $Action
        }
        catch {
            if ($attempt -ge $MaxAttempts) {
                throw
            }

            Write-Host "Tentativa $attempt/$MaxAttempts falhou em '$Description'. Aguardando $DelayMilliseconds ms..."
            Start-Sleep -Milliseconds $DelayMilliseconds
        }
    }
}

function Get-FileSha256 {
    param([string]$Path)

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        $hasher = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hashBytes = $hasher.ComputeHash($stream)
        }
        finally {
            $hasher.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    return ([System.BitConverter]::ToString($hashBytes)).Replace("-", "").ToLowerInvariant()
}

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
        [string]$ReleasePageUrl,
        [string]$ReleaseNotesUrl,
        [string]$SupportUrl,
        [string]$UpdatedAtUtc
    )

    if (-not (Test-Path $FilePath)) {
        throw "Arquivo update-info nao encontrado: $FilePath"
    }

    $json = Get-Content $FilePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $json.currentVersion = $Version
    $json.installerAssetName = $InstallerAssetName
    $json.releasePageUrl = $ReleasePageUrl
    $json.releaseNotesUrl = $ReleaseNotesUrl
    $json.supportUrl = $SupportUrl
    $json.updatedAtUtc = $UpdatedAtUtc

    $serialized = ($json | ConvertTo-Json -Depth 10).Replace("`r`n", "`n")
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($FilePath, $serialized, $utf8NoBom)
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
        [string]$GitHubOwner,
        [string]$GitHubRepo,
        [string]$PrepareInput,
        [switch]$SignInstaller,
        [switch]$SignExecutable,
        [string]$CertThumbprint,
        [string]$PfxPath,
        [string]$PfxPassword,
        [string]$CertStoreScope,
        [string]$TimestampUrl
    )

    $invokeArgs = @{
        Configuration = $Configuration
        Platform = $Platform
        RuntimeIdentifier = $RuntimeIdentifier
        SelfContained = $SelfContained
        WindowsAppSDKSelfContained = $WindowsAppSDKSelfContained
        OutputPath = $OutputPath
        SetupBaseName = $SetupBaseName
        InstallWebView2 = $InstallWebView2
        InstallDotNetDesktopRuntime = $InstallDotNetDesktopRuntime
        DotNetDesktopRuntimeMajor = $DotNetDesktopRuntimeMajor
        GitHubOwner = $GitHubOwner
        GitHubRepo = $GitHubRepo
        PrepareInput = $PrepareInput
    }

    if ($SignInstaller) {
        $invokeArgs.SignInstaller = $true
    }

    if ($SignExecutable) {
        $invokeArgs.SignExecutable = $true
    }

    if (-not [string]::IsNullOrWhiteSpace($CertThumbprint)) {
        $invokeArgs.CertThumbprint = $CertThumbprint
    }

    if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
        $invokeArgs.PfxPath = $PfxPath
        $invokeArgs.PfxPassword = $PfxPassword
    }

    if (-not [string]::IsNullOrWhiteSpace($CertStoreScope)) {
        $invokeArgs.CertStoreScope = $CertStoreScope
    }

    if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
        $invokeArgs.TimestampUrl = $TimestampUrl
    }

    $global:LASTEXITCODE = 0
    & $BuildScript @invokeArgs
    $exitCode = $LASTEXITCODE

    if (-not $?) {
        throw "Falha ao gerar instalador: $SetupBaseName"
    }

    if ($exitCode -ne 0) {
        throw "Falha ao gerar instalador: $SetupBaseName"
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$repoLinks = Get-DdsRepoLinks -RepoRoot $repoRoot -Owner $GitHubOwner -Repo $GitHubRepo
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
    -GitHubOwner $repoLinks.Owner `
    -GitHubRepo $repoLinks.Repo `
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
        -GitHubOwner $repoLinks.Owner `
        -GitHubRepo $repoLinks.Repo `
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
    Invoke-WithRetry -Description "Compress-Archive portable" -MaxAttempts 180 -DelayMilliseconds 1000 -Action {
        Compress-Archive -Path (Join-Path $installerInputAppPath "*") -DestinationPath $portableZipPath -CompressionLevel Optimal -Force
    } | Out-Null
}

$updatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
$stableUpdateInfo = Join-Path $repoRoot "installer\update\stable\update-info.json"
$betaUpdateInfo = Join-Path $repoRoot "installer\update\beta\update-info.json"

Update-UpdateInfo `
    -FilePath $stableUpdateInfo `
    -Version $stableVersion `
    -InstallerAssetName "$StableSetupBaseName.exe" `
    -ReleasePageUrl $repoLinks.LatestReleaseUrl `
    -ReleaseNotesUrl $repoLinks.ReleaseNotesUrl `
    -SupportUrl $repoLinks.SupportUrl `
    -UpdatedAtUtc $updatedAtUtc
if (-not $SkipBeta) {
    Update-UpdateInfo `
        -FilePath $betaUpdateInfo `
        -Version $effectiveBetaVersion `
        -InstallerAssetName "$BetaSetupBaseName.exe" `
        -ReleasePageUrl $repoLinks.ReleasesUrl `
        -ReleaseNotesUrl $repoLinks.ReleaseNotesUrl `
        -SupportUrl $repoLinks.SupportUrl `
        -UpdatedAtUtc $updatedAtUtc
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
    $hash = Invoke-WithRetry -Description "SHA256 $artifact" -MaxAttempts 180 -DelayMilliseconds 1000 -Action {
        Get-FileSha256 -Path $artifact
    }
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
