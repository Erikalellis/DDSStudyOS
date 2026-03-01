param(
    [string]$ProfilePath = "release-profile.local.ps1",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

$ErrorActionPreference = "Stop"

$releaseScript = Join-Path $PSScriptRoot "build-release-package.ps1"
if (-not (Test-Path $releaseScript)) {
    throw "Script nao encontrado: $releaseScript"
}

if ([System.IO.Path]::IsPathRooted($ProfilePath)) {
    $resolvedProfilePath = $ProfilePath
}
else {
    $scriptRelativePath = Join-Path $PSScriptRoot $ProfilePath
    if (Test-Path $scriptRelativePath) {
        $resolvedProfilePath = $scriptRelativePath
    }
    else {
        $resolvedProfilePath = Join-Path (Get-Location) $ProfilePath
    }
}

if (-not (Test-Path $resolvedProfilePath)) {
    $exampleProfilePath = Join-Path $PSScriptRoot "release-profile.example.ps1"
    throw "Profile nao encontrado: $resolvedProfilePath`nCrie o profile local a partir de: $exampleProfilePath"
}

$resolvedProfilePath = (Resolve-Path $resolvedProfilePath).Path
$releaseProfile = & $resolvedProfilePath

if (-not ($releaseProfile -is [System.Collections.IDictionary])) {
    throw "Profile invalido: o arquivo deve retornar um hashtable de parametros."
}

$releaseCommand = Get-Command $releaseScript -ErrorAction Stop
$allowedKeys = @{}
foreach ($parameterName in $releaseCommand.Parameters.Keys) {
    $allowedKeys[$parameterName] = $true
}

$releaseParams = @{}
foreach ($key in $releaseProfile.Keys) {
    if ($allowedKeys.ContainsKey([string]$key)) {
        $releaseParams[$key] = $releaseProfile[$key]
        continue
    }

    if ([string]::Equals([string]$key, "GitHubOwner", [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $releaseParams.ContainsKey("DistributionGitHubOwner")) {
        $releaseParams["DistributionGitHubOwner"] = $releaseProfile[$key]
        continue
    }

    if ([string]::Equals([string]$key, "GitHubRepo", [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $releaseParams.ContainsKey("DistributionGitHubRepo")) {
        $releaseParams["DistributionGitHubRepo"] = $releaseProfile[$key]
        continue
    }
}

Write-Host "==> Usando profile: $resolvedProfilePath"
& $releaseScript @releaseParams @ExtraArgs
if (-not $?) {
    throw "Falha na execucao do build-release-package.ps1."
}
