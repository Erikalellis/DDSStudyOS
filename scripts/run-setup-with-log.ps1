param(
    [string]$SetupPath = "artifacts\installer-output\DDSStudyOS-Setup.exe",
    [string]$LogDirectory = "artifacts\installer-logs",
    [ValidateSet("auto", "inno", "advanced")]
    [string]$Mode = "auto",
    [switch]$NoPrereqs,
    [switch]$Quiet,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedSetupPath = if ([System.IO.Path]::IsPathRooted($SetupPath)) { $SetupPath } else { Join-Path $repoRoot $SetupPath }
$resolvedLogDirectory = if ([System.IO.Path]::IsPathRooted($LogDirectory)) { $LogDirectory } else { Join-Path $repoRoot $LogDirectory }

if (-not (Test-Path $resolvedSetupPath)) {
    throw "Setup nao encontrado: $resolvedSetupPath"
}

if (-not (Test-Path $resolvedLogDirectory)) {
    New-Item -ItemType Directory -Path $resolvedLogDirectory -Force | Out-Null
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$resolvedMode = $Mode
if ($resolvedMode -eq "auto") {
    $setupFileName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedSetupPath).ToLowerInvariant()
    if ($setupFileName -like "*advanced*" -or $setupFileName -like "*msi*") {
        $resolvedMode = "advanced"
    }
    else {
        $resolvedMode = "inno"
    }
}

$args = @()
$logPaths = @()

if ($resolvedMode -eq "inno") {
    $innoLog = Join-Path $resolvedLogDirectory "setup-$stamp-inno.log"
    $args = @("/LOG=`"$innoLog`"")
    $logPaths += $innoLog

    if ($NoPrereqs) {
        Write-Warning "Parametro -NoPrereqs nao se aplica ao setup oficial. Recompile com -InstallWebView2 0 e -InstallDotNetDesktopRuntime 0 se precisar."
    }

    if ($Quiet) {
        $args += @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART")
    }
}
else {
    $bootstrapperLog = Join-Path $resolvedLogDirectory "setup-$stamp-bootstrapper.log"
    $msiLog = Join-Path $resolvedLogDirectory "setup-$stamp-msi.log"
    $args = @(
        "/exelog", "`"$bootstrapperLog`"",
        "/L*V", "`"$msiLog`""
    )

    if ($NoPrereqs) {
        $args += "/noprereqs"
    }

    if ($Quiet) {
        $args += @("/exenoui", "/qn", "/norestart")
    }

    $logPaths += @($bootstrapperLog, $msiLog)
}

Write-Host "==> Executando setup com logs"
Write-Host "Setup: $resolvedSetupPath"
Write-Host "Modo: $resolvedMode"
Write-Host "Argumentos: $($args -join ' ')"
Write-Host "Logs:"
foreach ($logPath in $logPaths) {
    Write-Host " - $logPath"
}

if ($DryRun) {
    Write-Host "DryRun habilitado. Nenhuma instalacao foi iniciada."
    exit 0
}

$process = Start-Process -FilePath $resolvedSetupPath -ArgumentList $args -PassThru -Wait

Write-Host ""
Write-Host "Processo finalizado. ExitCode: $($process.ExitCode)"
Write-Host "Logs:"
foreach ($logPath in $logPaths) {
    Write-Host " - $logPath"
}

if ($process.ExitCode -ne 0) {
    exit $process.ExitCode
}
