param(
    [string]$SetupPath = "artifacts\installer-output\DDSStudyOS-Setup.exe",
    [string]$LogDirectory = "artifacts\installer-logs",
    [string]$InstallDir = "C:\Program Files\Deep Darkness Studios\DDS StudyOS",
    [int]$StartupSeconds = 12,
    [switch]$RunSetup,
    [switch]$KeepInstalled
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedSetupPath = if ([System.IO.Path]::IsPathRooted($SetupPath)) { $SetupPath } else { Join-Path $repoRoot $SetupPath }
$resolvedLogDirectory = if ([System.IO.Path]::IsPathRooted($LogDirectory)) { $LogDirectory } else { Join-Path $repoRoot $LogDirectory }

if (-not (Test-Path $resolvedLogDirectory)) {
    New-Item -ItemType Directory -Path $resolvedLogDirectory -Force | Out-Null
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $resolvedLogDirectory "clean-machine-smoke-$stamp.txt"
$setupLog = Join-Path $resolvedLogDirectory "clean-machine-setup-$stamp-inno.log"
$uninstallLog = Join-Path $resolvedLogDirectory "clean-machine-uninstall-$stamp.log"

function Add-ReportLine([string]$line) {
    $line | Out-File -FilePath $reportPath -Append -Encoding utf8
    Write-Host $line
}

function Find-Shortcut([string[]]$candidates) {
    foreach ($path in $candidates) {
        if (Test-Path $path) { return $path }
    }
    return $null
}

Add-ReportLine "DDS StudyOS - clean machine smoke"
Add-ReportLine "Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Add-ReportLine "RunSetup: $RunSetup"

if ($RunSetup) {
    if (-not (Test-Path $resolvedSetupPath)) {
        throw "Setup nao encontrado: $resolvedSetupPath"
    }

    Add-ReportLine "[INFO] Executando setup silencioso..."
    $setupArgs = @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/LOG=$setupLog")
    $setupProcess = Start-Process -FilePath $resolvedSetupPath -ArgumentList $setupArgs -PassThru -Wait
    if ($setupProcess.ExitCode -ne 0) {
        Add-ReportLine "[FAIL] Setup falhou. ExitCode=$($setupProcess.ExitCode)"
        Add-ReportLine "SetupLog: $setupLog"
        exit $setupProcess.ExitCode
    }
    Add-ReportLine "[OK] Setup concluido com sucesso."
    Add-ReportLine "SetupLog: $setupLog"
}

$appExe = Join-Path $InstallDir "app\DDSStudyOS.App.exe"
$uninsExe = Join-Path $InstallDir "unins000.exe"

if (-not (Test-Path $appExe)) {
    Add-ReportLine "[FAIL] Executavel principal nao encontrado: $appExe"
    Add-ReportLine "Report: $reportPath"
    exit 2
}
Add-ReportLine "[OK] Executavel principal encontrado."

$startMenuCandidates = @(
    "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\DDS StudyOS.lnk",
    (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\DDS StudyOS.lnk")
)
$desktopCandidates = @(
    "C:\Users\Public\Desktop\DDS StudyOS.lnk",
    (Join-Path $env:USERPROFILE "Desktop\DDS StudyOS.lnk")
)

$startMenuShortcut = Find-Shortcut $startMenuCandidates
$desktopShortcut = Find-Shortcut $desktopCandidates

if ($startMenuShortcut) {
    Add-ReportLine "[OK] Atalho Menu Iniciar encontrado: $startMenuShortcut"
} else {
    Add-ReportLine "[WARN] Atalho Menu Iniciar nao encontrado."
}

if ($desktopShortcut) {
    Add-ReportLine "[INFO] Atalho Desktop encontrado: $desktopShortcut"
} else {
    Add-ReportLine "[INFO] Atalho Desktop nao encontrado."
}

Add-ReportLine "[INFO] Executando smoke de abertura por $StartupSeconds segundos..."
$appProcess = Start-Process -FilePath $appExe -PassThru
Start-Sleep -Seconds $StartupSeconds

if ($appProcess.HasExited) {
    Add-ReportLine "[FAIL] App encerrou durante o smoke test."
    Add-ReportLine "Report: $reportPath"
    exit 3
}

Add-ReportLine "[OK] App permaneceu ativa durante o smoke test."
Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

if ($RunSetup -and -not $KeepInstalled) {
    if (Test-Path $uninsExe) {
        Add-ReportLine "[INFO] Executando uninstall silencioso..."
        $uninstallArgs = @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/LOG=$uninstallLog")
        $uninstallProcess = Start-Process -FilePath $uninsExe -ArgumentList $uninstallArgs -PassThru -Wait
        if ($uninstallProcess.ExitCode -ne 0) {
            Add-ReportLine "[WARN] Uninstall retornou ExitCode=$($uninstallProcess.ExitCode)"
        } else {
            Add-ReportLine "[OK] Uninstall concluido com sucesso."
        }
        Add-ReportLine "UninstallLog: $uninstallLog"
    } else {
        Add-ReportLine "[WARN] Uninstaller nao encontrado para limpeza final."
    }
}

Add-ReportLine "[OK] Validacao finalizada."
Add-ReportLine "Report: $reportPath"
