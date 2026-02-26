param(
    [ValidateSet("stable", "beta")]
    [string]$Channel = "stable",
    [string]$ModuleRoot = "dlc/modules",
    [string]$OutputPath = "artifacts/dlc-output",
    [string]$ManifestPath = "",
    [string]$Owner = "Erikalellis",
    [string]$Repo = "DDSStudyOS",
    [string]$ReleaseTag = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedModuleRoot = if ([System.IO.Path]::IsPathRooted($ModuleRoot)) { $ModuleRoot } else { Join-Path $repoRoot $ModuleRoot }
$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = "installer/update/$Channel/dlc-manifest.json"
}

$resolvedManifestPath = if ([System.IO.Path]::IsPathRooted($ManifestPath)) { $ManifestPath } else { Join-Path $repoRoot $ManifestPath }

if (-not (Test-Path $resolvedModuleRoot)) {
    throw "Pasta de modulos nao encontrada: $resolvedModuleRoot"
}

if (-not (Test-Path $resolvedOutputPath)) {
    New-Item -ItemType Directory -Path $resolvedOutputPath -Force | Out-Null
}

$manifestDirectory = Split-Path -Path $resolvedManifestPath -Parent
if (-not (Test-Path $manifestDirectory)) {
    New-Item -ItemType Directory -Path $manifestDirectory -Force | Out-Null
}

function Resolve-AppVersion {
    param([string]$RepoRoot)

    $projectFile = Join-Path $RepoRoot "src/DDSStudyOS.App/DDSStudyOS.App.csproj"
    if (-not (Test-Path $projectFile)) {
        throw "Projeto nao encontrado: $projectFile"
    }

    [xml]$xml = Get-Content $projectFile -Raw -Encoding UTF8
    $rawVersion = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($rawVersion)) {
        throw "Tag <Version> nao encontrada em: $projectFile"
    }

    return ($rawVersion -split '[\+\-]')[0]
}

function Normalize-ModuleId {
    param([string]$RawValue)

    $value = $RawValue.Trim().ToLowerInvariant()
    $value = [Regex]::Replace($value, "[^a-z0-9\-_.]", "-")
    $value = [Regex]::Replace($value, "-{2,}", "-")
    return $value.Trim('-')
}

$appVersion = Resolve-AppVersion -RepoRoot $repoRoot
$generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")

Write-Host "==> Build DLC package"
Write-Host "Channel: $Channel"
Write-Host "Module root: $resolvedModuleRoot"
Write-Host "Output: $resolvedOutputPath"
Write-Host "Manifest: $resolvedManifestPath"

$moduleDirectories = Get-ChildItem -Path $resolvedModuleRoot -Directory | Sort-Object Name
$modules = @()

foreach ($moduleDir in $moduleDirectories) {
    $moduleConfigPath = Join-Path $moduleDir.FullName "module.json"
    $config = $null

    if (Test-Path $moduleConfigPath) {
        $config = Get-Content $moduleConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }

    $moduleId = if ($config -and -not [string]::IsNullOrWhiteSpace($config.id)) {
        Normalize-ModuleId ([string]$config.id)
    }
    else {
        Normalize-ModuleId $moduleDir.Name
    }

    if ([string]::IsNullOrWhiteSpace($moduleId)) {
        Write-Warning "Modulo ignorado (id invalido): $($moduleDir.FullName)"
        continue
    }

    $enabled = $true
    if ($config -and $config.PSObject.Properties.Match("enabled").Count -gt 0) {
        $enabled = [bool]$config.enabled
    }

    if (-not $enabled) {
        Write-Host "Modulo desabilitado no module.json: $moduleId"
        continue
    }

    $moduleVersion = if ($config -and -not [string]::IsNullOrWhiteSpace($config.version)) {
        ([string]$config.version).Trim()
    }
    else {
        $appVersion
    }

    $extractSubdirectory = if ($config -and -not [string]::IsNullOrWhiteSpace($config.extractSubdirectory)) {
        ([string]$config.extractSubdirectory).Trim()
    }
    else {
        $moduleId
    }

    $assetName = if ($config -and -not [string]::IsNullOrWhiteSpace($config.assetName)) {
        ([string]$config.assetName).Trim()
    }
    else {
        "DDSStudyOS-DLC-$moduleId.zip"
    }

    if (-not $assetName.EndsWith(".zip", [System.StringComparison]::OrdinalIgnoreCase)) {
        $assetName += ".zip"
    }

    $zipPath = Join-Path $resolvedOutputPath $assetName
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $moduleDir.FullName "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force

    $sha256 = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $sizeBytes = (Get-Item $zipPath).Length

    $downloadUrl = if ($config -and -not [string]::IsNullOrWhiteSpace($config.downloadUrl)) {
        ([string]$config.downloadUrl).Trim()
    }
    elseif (-not [string]::IsNullOrWhiteSpace($ReleaseTag)) {
        "https://github.com/$Owner/$Repo/releases/download/$ReleaseTag/$assetName"
    }
    else {
        "https://github.com/$Owner/$Repo/releases/latest/download/$assetName"
    }

    $moduleDescription = if ($config -and -not [string]::IsNullOrWhiteSpace($config.description)) {
        ([string]$config.description).Trim()
    }
    else {
        ""
    }

    $required = $false
    if ($config -and $config.PSObject.Properties.Match("required").Count -gt 0) {
        $required = [bool]$config.required
    }

    Write-Host "- Modulo: $moduleId ($moduleVersion)"

    $modules += [pscustomobject]@{
        id = $moduleId
        version = $moduleVersion
        assetName = $assetName
        downloadUrl = $downloadUrl
        sha256 = $sha256
        sizeBytes = $sizeBytes
        extractSubdirectory = $extractSubdirectory
        required = $required
        description = $moduleDescription
        enabled = $true
    }
}

$manifest = [pscustomobject]@{
    channel = $Channel
    product = "DDS StudyOS"
    appVersion = $appVersion
    minimumAppVersion = $appVersion
    manifestVersion = 1
    generatedAtUtc = $generatedAtUtc
    releaseTag = $ReleaseTag
    modules = $modules
}

$json = ($manifest | ConvertTo-Json -Depth 10).Replace("`r`n", "`n")
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedManifestPath, $json, $utf8NoBom)

Write-Host ""
Write-Host "Manifesto DLC atualizado: $resolvedManifestPath"
Write-Host "Modulos empacotados: $($modules.Count)"
Write-Host "Pasta de saida: $resolvedOutputPath"
