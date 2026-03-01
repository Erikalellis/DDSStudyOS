param(
    [string]$DistributionGitHubOwner = "Erikalellis",
    [string]$DistributionGitHubRepo = "DDSStudyOS-Updates",
    [string]$Branch = "main",
    [string]$TempRoot = "artifacts\\temp-distribution-publish",
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

function Ensure-CleanDirectory {
    param([string]$Path)

    if (Test-Path $Path) {
        Remove-Item $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedTempRoot = if ([System.IO.Path]::IsPathRooted($TempRoot)) { $TempRoot } else { Join-Path $repoRoot $TempRoot }
$checkoutPath = Join-Path $resolvedTempRoot "distribution-repo"
$fullRepo = "$DistributionGitHubOwner/$DistributionGitHubRepo"
$gh = Resolve-GhPath

Ensure-GhAuth -GhPath $gh
Ensure-CleanDirectory -Path $resolvedTempRoot

if ($CreateIfMissing) {
    cmd /c "`"$gh`" repo view $fullRepo >nul 2>nul"
    if ($LASTEXITCODE -ne 0) {
        & $gh repo create $fullRepo --public --description "DDS StudyOS public update channel"
        if ($LASTEXITCODE -ne 0) {
            throw "Falha ao criar repositorio publico de distribuicao: $fullRepo"
        }
    }
}

git clone "https://github.com/$fullRepo.git" $checkoutPath | Out-Null

$targets = @(
    @{ Source = Join-Path $repoRoot "installer\update\stable\update-info.json"; Destination = Join-Path $checkoutPath "installer\update\stable\update-info.json" },
    @{ Source = Join-Path $repoRoot "installer\update\stable\dlc-manifest.json"; Destination = Join-Path $checkoutPath "installer\update\stable\dlc-manifest.json" },
    @{ Source = Join-Path $repoRoot "installer\update\beta\update-info.json"; Destination = Join-Path $checkoutPath "installer\update\beta\update-info.json" },
    @{ Source = Join-Path $repoRoot "installer\update\beta\dlc-manifest.json"; Destination = Join-Path $checkoutPath "installer\update\beta\dlc-manifest.json" }
)

foreach ($target in $targets) {
    if (-not (Test-Path $target.Source)) {
        throw "Arquivo de manifesto nao encontrado: $($target.Source)"
    }

    $destinationDir = Split-Path -Path $target.Destination -Parent
    if (-not (Test-Path $destinationDir)) {
        New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
    }

    Copy-Item -Path $target.Source -Destination $target.Destination -Force
}

Push-Location $checkoutPath
try {
    cmd /c "git rev-parse --verify HEAD >nul 2>nul"
    if ($LASTEXITCODE -ne 0) {
        git checkout --orphan $Branch | Out-Null
    }
    else {
        git checkout $Branch | Out-Null
    }

    git add installer/update/stable/update-info.json installer/update/stable/dlc-manifest.json installer/update/beta/update-info.json installer/update/beta/dlc-manifest.json

    $status = git status --short
    if (-not [string]::IsNullOrWhiteSpace($status)) {
        git commit -m "chore: sync public update manifests"
        git push origin $Branch
    }
}
finally {
    Pop-Location
}

Write-Host "Manifestos sincronizados para $fullRepo ($Branch)."
