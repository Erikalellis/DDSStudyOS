param(
    [string]$AppExePath = "C:\Program Files\Deep Darkness Studios\DDS StudyOS\app\DDSStudyOS.App.exe",
    [string]$LogDirectory = "artifacts\installer-logs",
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedLogDirectory = if ([System.IO.Path]::IsPathRooted($LogDirectory)) { $LogDirectory } else { Join-Path $repoRoot $LogDirectory }
$resolvedAppExe = if ([System.IO.Path]::IsPathRooted($AppExePath)) { $AppExePath } else { Join-Path $repoRoot $AppExePath }

if (-not (Test-Path $resolvedLogDirectory)) {
    New-Item -ItemType Directory -Path $resolvedLogDirectory -Force | Out-Null
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $resolvedLogDirectory "first-use-smoke-$stamp.txt"

function Add-Report([string]$line) {
    $line | Out-File -FilePath $reportPath -Append -Encoding utf8
    Write-Host $line
}

Add-Report "DDS StudyOS - first-use smoke"
Add-Report "Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Add-Report "AppExe: $resolvedAppExe"

if (-not (Test-Path $resolvedAppExe)) {
    Add-Report "[FAIL] Executavel nao encontrado: $resolvedAppExe"
    Add-Report "Report: $reportPath"
    exit 2
}

$localDataDir = Join-Path $env:LOCALAPPDATA "DDSStudyOS"
$profilePath = Join-Path $localDataDir "config\user-profile.json"
$appLogPath = Join-Path $localDataDir "logs\app.log"

$existing = Get-Process -Name "DDSStudyOS.App" -ErrorAction SilentlyContinue
if ($existing) {
    Add-Report "[INFO] Encerrando instancia existente do app."
    $existing | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

if (Test-Path $localDataDir) {
    Add-Report "[INFO] Limpando pasta local para simular primeiro uso: $localDataDir"
    Remove-Item -Path $localDataDir -Recurse -Force -ErrorAction SilentlyContinue
}

Add-Report "[INFO] Iniciando app com argumento --smoke-first-use"
$proc = Start-Process -FilePath $resolvedAppExe -ArgumentList "--smoke-first-use" -PassThru

$deadline = (Get-Date).AddSeconds([Math]::Max(15, $TimeoutSeconds))
while (-not $proc.HasExited -and (Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 500
}

if (-not $proc.HasExited) {
    Add-Report "[FAIL] Timeout aguardando encerramento do smoke mode ($TimeoutSeconds s)."
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Add-Report "Report: $reportPath"
    exit 3
}

Add-Report "[OK] App encerrou automaticamente no modo smoke. ExitCode=$($proc.ExitCode)"

if (-not (Test-Path $appLogPath)) {
    Add-Report "[FAIL] Log do app nao encontrado: $appLogPath"
    Add-Report "Report: $reportPath"
    exit 4
}

$logText = Get-Content $appLogPath -Raw -Encoding UTF8

$requiredMarkers = @(
    "SMOKE_FIRST_USE:MODE_ENABLED",
    "SMOKE_FIRST_USE:ONBOARDING_OK",
    "SMOKE_FIRST_USE:TOUR_OK",
    "SMOKE_FIRST_USE:BROWSER_HOME_OK",
    "SMOKE_FIRST_USE:BROWSER_OK",
    "SMOKE_FIRST_USE:SUCCESS"
)

$missing = @()
foreach ($marker in $requiredMarkers) {
    if ($logText -match [regex]::Escape($marker)) {
        Add-Report "[OK] Marker encontrado: $marker"
    }
    else {
        Add-Report "[FAIL] Marker ausente: $marker"
        $missing += $marker
    }
}

if (-not (Test-Path $profilePath)) {
    Add-Report "[FAIL] Perfil nao encontrado apos smoke: $profilePath"
    Add-Report "Report: $reportPath"
    exit 5
}

try {
    $profile = Get-Content $profilePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $profileName = [string]$profile.Name
    $profileTour = [bool]$profile.HasSeenTour

    if ([string]::IsNullOrWhiteSpace($profileName)) {
        Add-Report "[FAIL] Perfil salvo sem nome."
        exit 6
    }

    if (-not $profileTour) {
        Add-Report "[FAIL] Perfil salvo sem HasSeenTour=true."
        exit 7
    }

    Add-Report "[OK] Perfil validado: Name='$profileName' HasSeenTour=$profileTour"
}
catch {
    Add-Report "[FAIL] Falha ao ler perfil salvo. Motivo: $($_.Exception.Message)"
    exit 8
}

if ($missing.Count -gt 0) {
    Add-Report "[FAIL] Smoke concluido com marcadores ausentes: $($missing -join ', ')"
    Add-Report "AppLog: $appLogPath"
    Add-Report "Report: $reportPath"
    exit 9
}

Add-Report "[OK] First-use smoke validado com sucesso."
Add-Report "AppLog: $appLogPath"
Add-Report "Report: $reportPath"
