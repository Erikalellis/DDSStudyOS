param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win10-x64",
    [bool]$WindowsAppSDKSelfContained = $false,
    [switch]$SkipSolutionBuild,
    [string]$MsBuildPath = ""
)

$ErrorActionPreference = "Stop"

function Resolve-MsBuildPath {
    if (-not [string]::IsNullOrWhiteSpace($MsBuildPath)) {
        if (-not (Test-Path $MsBuildPath)) {
            throw "MSBuild nao encontrado no caminho informado: $MsBuildPath"
        }
        return (Resolve-Path $MsBuildPath).Path
    }

    $cmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null
        if ($found -and (Test-Path $found)) {
            return $found
        }
    }

    throw "MSBuild nao encontrado. Instale Visual Studio 2022/2026 com workload de desktop Windows."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionPath = Join-Path $repoRoot "DDSStudyOS.sln"
$projectPath = Join-Path $repoRoot "src\DDSStudyOS.App\DDSStudyOS.App.csproj"

if (-not (Test-Path $solutionPath)) {
    throw "Solution nao encontrada: $solutionPath"
}
if (-not (Test-Path $projectPath)) {
    throw "Projeto nao encontrado: $projectPath"
}

$resolvedMsBuild = Resolve-MsBuildPath
Write-Host "MSBuild: $resolvedMsBuild"
Write-Host "Configuration: $Configuration"
Write-Host "RuntimeIdentifier: $RuntimeIdentifier"
Write-Host "WindowsAppSDKSelfContained: $WindowsAppSDKSelfContained"

if (-not $SkipSolutionBuild) {
    Write-Host ""
    Write-Host "==> Build da solution"
    & $resolvedMsBuild $solutionPath /t:Build /p:Configuration=$Configuration /m /nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Falha no build da solution."
    }
}

Write-Host ""
Write-Host "==> Publish do app"
& $resolvedMsBuild $projectPath /t:Publish /p:Configuration=$Configuration /p:RuntimeIdentifier=$RuntimeIdentifier /p:WindowsAppSDKSelfContained=$WindowsAppSDKSelfContained /m /nologo
if ($LASTEXITCODE -ne 0) {
    throw "Falha no publish do app."
}

$publishPath = Join-Path $repoRoot "src\DDSStudyOS.App\bin\$Configuration\net8.0-windows10.0.19041.0\$RuntimeIdentifier\publish"
Write-Host ""
Write-Host "Publish concluido em: $publishPath"
Write-Host "Observacao: o publish recria o EXE. Assine novamente com scripts/sign-release.ps1."
