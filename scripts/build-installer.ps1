param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$RuntimeIdentifier = "win10-x64",
    [string]$ProjectPath = "installer\advanced-installer\DDSStudyOS.aip",
    [string]$OutputPath = "artifacts\installer-output",
    [string]$BuildName = "DefaultBuild",
    [string]$SetupFileName = "DDSStudyOS-Setup.exe",
    [string]$AdvancedInstallerPath = "",
    [switch]$SignExecutable,
    [string]$CertThumbprint = "6780CE530A33615B591727F5334B3DD075B76422",
    [string]$SelfContained = "true",
    [string]$WindowsAppSDKSelfContained = "true",
    [string]$EnableDotNetDesktopPrerequisite = "true",
    [string]$DotNetDesktopPrerequisiteName = ".NET Desktop Runtime 8 (x64)",
    [string]$DotNetDesktopRuntimeUrl = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe",
    [string]$DotNetDesktopMinVersion = "8.0.0"
)

$ErrorActionPreference = "Stop"

function Resolve-AiCliPath {
    param([string]$PathOverride)

    if (-not [string]::IsNullOrWhiteSpace($PathOverride)) {
        if (-not (Test-Path $PathOverride)) {
            throw "AdvancedInstaller.com nao encontrado no caminho informado: $PathOverride"
        }
        return (Resolve-Path $PathOverride).Path
    }

    $candidates = @(
        "C:\Program Files (x86)\Caphyon\Advanced Installer*\bin\x86\AdvancedInstaller.com",
        "C:\Program Files\Caphyon\Advanced Installer*\bin\x86\AdvancedInstaller.com",
        "F:\Program Files (x86)\Caphyon\Advanced Installer*\bin\x86\AdvancedInstaller.com",
        "F:\Program Files\Caphyon\Advanced Installer*\bin\x86\AdvancedInstaller.com"
    )

    foreach ($pattern in $candidates) {
        $match = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
        if ($match) {
            return $match.FullName
        }
    }

    $cmd = Get-Command AdvancedInstaller.com -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    throw "Advanced Installer nao encontrado. Instale e execute novamente."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$createScript = Join-Path $PSScriptRoot "create-advanced-installer-project.ps1"
if (-not (Test-Path $createScript)) {
    throw "Script nao encontrado: $createScript"
}

$resolvedAipPath = if ([System.IO.Path]::IsPathRooted($ProjectPath)) { $ProjectPath } else { Join-Path $repoRoot $ProjectPath }
$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
$aiCli = Resolve-AiCliPath -PathOverride $AdvancedInstallerPath

Write-Host "==> Recriando projeto do instalador com arquivos atualizados"
$createArgs = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $createScript,
    "-PrepareInput",
    "-Force",
    "-Configuration", $Configuration,
    "-Platform", $Platform,
    "-RuntimeIdentifier", $RuntimeIdentifier,
    "-ProjectPath", $resolvedAipPath,
    "-OutputPath", $resolvedOutputPath,
    "-BuildName", $BuildName,
    "-SetupFileName", $SetupFileName,
    "-SelfContained", $SelfContained,
    "-WindowsAppSDKSelfContained", $WindowsAppSDKSelfContained,
    "-EnableDotNetDesktopPrerequisite", $EnableDotNetDesktopPrerequisite,
    "-DotNetDesktopPrerequisiteName", $DotNetDesktopPrerequisiteName,
    "-DotNetDesktopRuntimeUrl", $DotNetDesktopRuntimeUrl,
    "-DotNetDesktopMinVersion", $DotNetDesktopMinVersion,
    "-AdvancedInstallerPath", $aiCli
)

if ($SignExecutable) {
    $createArgs += "-SignExecutable"
    if (-not [string]::IsNullOrWhiteSpace($CertThumbprint)) {
        $createArgs += @("-CertThumbprint", $CertThumbprint)
    }
}

powershell @createArgs
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao recriar o projeto do instalador."
}

Write-Host "==> Compilando setup no Advanced Installer"
& $aiCli /build $resolvedAipPath -buildslist $BuildName
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Falha ao compilar build especifico '$BuildName'. Tentando build padrao..."
    & $aiCli /build $resolvedAipPath
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao compilar o instalador no Advanced Installer."
    }
}

$setupPath = Join-Path $resolvedOutputPath $SetupFileName
if (-not (Test-Path $setupPath)) {
    $fallback = Get-ChildItem -Path $resolvedOutputPath -Filter "*.exe" -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($fallback) {
        $setupPath = $fallback.FullName
    }
}

if (-not (Test-Path $setupPath)) {
    throw "Build finalizado, mas setup nao encontrado em: $resolvedOutputPath"
}

Write-Host ""
Write-Host "Setup gerado com sucesso:"
Write-Host $setupPath
