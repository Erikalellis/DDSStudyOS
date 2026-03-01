param(
    [string]$DistributionGitHubOwner = "Erikalellis",
    [string]$DistributionGitHubRepo = "DDSStudyOS-Updates",
    [string]$ReleaseTag = "",
    [string]$OutputPath = "artifacts\\installer-output",
    [string]$StableSetupBaseName = "DDSStudyOS-Setup",
    [string]$BetaSetupBaseName = "DDSStudyOS-Beta-Setup",
    [string]$PortableBaseName = "DDSStudyOS-Portable",
    [string]$ShaFileName = "DDSStudyOS-SHA256.txt",
    [switch]$CreateIfMissing
)

$ErrorActionPreference = "Stop"
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

function Resolve-GhPath {
    $paths = @(
        "C:\Program Files\GitHub CLI\gh.exe",
        "C:\Program Files (x86)\GitHub CLI\gh.exe"
    )
    foreach ($p in $paths) {
        if (Test-Path $p) { return $p }
    }
    $cmd = Get-Command gh.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "GitHub CLI (gh) nao encontrado."
}

function Ensure-GhAuth {
    param([string]$GhPath)

    cmd /c "`"$GhPath`" auth status >nul 2>nul"
    if ($LASTEXITCODE -ne 0) {
        throw "Nao autenticado no GitHub. Rode: gh auth login --hostname github.com --git-protocol https --web"
    }
}

function Resolve-AppVersion {
    param([string]$RepoRoot)

    $projectFile = Join-Path $RepoRoot "src\DDSStudyOS.App\DDSStudyOS.App.csproj"
    [xml]$xml = Get-Content $projectFile -Raw -Encoding UTF8
    $rawVersion = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($rawVersion)) {
        throw "Tag <Version> nao encontrada em: $projectFile"
    }

    return ($rawVersion -split '[\+\-]')[0]
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$gh = Resolve-GhPath
$fullRepo = "$DistributionGitHubOwner/$DistributionGitHubRepo"
$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }

Ensure-GhAuth -GhPath $gh

if ($CreateIfMissing) {
    cmd /c "`"$gh`" repo view $fullRepo >nul 2>nul"
    if ($LASTEXITCODE -ne 0) {
        & $gh repo create $fullRepo --public --description "DDS StudyOS public update channel"
        if ($LASTEXITCODE -ne 0) {
            throw "Falha ao criar repositorio publico de distribuicao: $fullRepo"
        }
    }
}

$resolvedTag = if ([string]::IsNullOrWhiteSpace($ReleaseTag)) { "v$(Resolve-AppVersion -RepoRoot $repoRoot)" } else { $ReleaseTag.Trim() }
$assets = @(
    (Join-Path $resolvedOutputPath "$StableSetupBaseName.exe")
    (Join-Path $resolvedOutputPath "$BetaSetupBaseName.exe")
    (Join-Path $resolvedOutputPath "$PortableBaseName.zip")
    (Join-Path $resolvedOutputPath $ShaFileName)
)

$missing = @($assets | Where-Object { -not (Test-Path $_) })
if ($missing.Count -gt 0) {
    throw "Artefatos ausentes para publicar: $($missing -join ', ')"
}

cmd /c "`"$gh`" release view $resolvedTag --repo $fullRepo >nul 2>nul"
if ($LASTEXITCODE -ne 0) {
    & $gh release create $resolvedTag @assets --repo $fullRepo --title $resolvedTag --notes "DDS StudyOS public distribution channel."
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao criar release $resolvedTag em $fullRepo"
    }
}
else {
    & $gh release upload $resolvedTag @assets --repo $fullRepo --clobber
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao atualizar assets da release $resolvedTag em $fullRepo"
    }
}

Write-Host "Assets publicados em $fullRepo ($resolvedTag)."
