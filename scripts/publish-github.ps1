param(
    [string]$RepoName = "DDSStudyOS",
    [ValidateSet("public", "private")]
    [string]$Visibility = "public",
    [string]$Owner = "",
    [string]$Description = "DDS StudyOS - WinUI 3 (.NET 8)",
    [string]$Homepage = "http://177.71.165.60/"
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

function Update-LinkPlaceholders {
    param(
        [string]$RepoRoot,
        [string]$OwnerName,
        [string]$Repository
    )

    $targets = @(
        (Join-Path $RepoRoot ".github\ISSUE_TEMPLATE\config.yml"),
        (Join-Path $RepoRoot "docs\README.md"),
        (Join-Path $RepoRoot "docs\UPDATE_INFO.md"),
        (Join-Path $RepoRoot "installer\update\stable\update-info.json"),
        (Join-Path $RepoRoot "installer\update\beta\update-info.json"),
        (Join-Path $RepoRoot "installer\inno\DDSStudyOS.iss"),
        (Join-Path $RepoRoot "installer\inno\DDSStudyOS-Personalizado.iss")
    )

    foreach ($file in $targets) {
        if (-not (Test-Path $file)) { continue }
        $content = Get-Content $file -Raw -Encoding UTF8
        $updated = $content.Replace("<OWNER>", $OwnerName).Replace("<REPO>", $Repository)
        if ($updated -ne $content) {
            Set-Content -Path $file -Value $updated -NoNewline -Encoding UTF8
            Write-Host "Atualizado: $file"
        }
    }
}

$gh = Resolve-GhPath
Ensure-GhAuth -GhPath $gh

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repoRoot
try {
    $remoteExists = (git remote) -contains "origin"

    if (-not $remoteExists) {
        $fullRepo = if ([string]::IsNullOrWhiteSpace($Owner)) { $RepoName } else { "$Owner/$RepoName" }
        $createArgs = @(
            "repo", "create", $fullRepo,
            "--$Visibility",
            "--source", ".",
            "--remote", "origin",
            "--push",
            "--description", $Description
        )
        if (-not [string]::IsNullOrWhiteSpace($Homepage)) {
            $createArgs += @("--homepage", $Homepage)
        }

        & $gh @createArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Falha ao criar/push inicial do repositorio no GitHub."
        }
    }
    else {
        git push -u origin main
    }

    $ownerName = & $gh repo view --json owner --jq ".owner.login"
    $repoName = & $gh repo view --json name --jq ".name"
    $repoUrl = & $gh repo view --json url --jq ".url"

    Update-LinkPlaceholders -RepoRoot $repoRoot -OwnerName $ownerName.Trim() -Repository $repoName.Trim()

    git -c core.safecrlf=false add ".github/ISSUE_TEMPLATE/config.yml" "docs/README.md" "docs/UPDATE_INFO.md" "installer/update/stable/update-info.json" "installer/update/beta/update-info.json" "installer/inno/DDSStudyOS.iss" "installer/inno/DDSStudyOS-Personalizado.iss" 2>$null
    if (-not [string]::IsNullOrWhiteSpace((git status --porcelain))) {
        git commit -m "docs: bind GitHub support/update links to repository URL"
        git push
    }

    Write-Host ""
    Write-Host "Repositorio publicado: $($repoUrl.Trim())"
    Write-Host "Support URL: $($repoUrl.Trim())/blob/main/SUPPORT.md"
    Write-Host "Update Info URL: $($repoUrl.Trim())/blob/main/docs/UPDATE_INFO.md"
    Write-Host "Release Notes URL: $($repoUrl.Trim())/blob/main/CHANGELOG.md"
}
finally {
    Pop-Location
}
