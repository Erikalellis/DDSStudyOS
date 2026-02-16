param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$RuntimeIdentifier = "win10-x64",
    [string]$SelfContained = "true",
    [string]$WindowsAppSDKSelfContained = "true",
    [switch]$SkipSolutionBuild,
    [string]$MsBuildPath = ""
)

$ErrorActionPreference = "Stop"

function ConvertTo-BoolValue {
    param(
        [string]$Value,
        [string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Parametro '$Name' nao pode ser vazio."
    }

    switch ($Value.Trim().ToLowerInvariant()) {
        "1" { return $true }
        "true" { return $true }
        "yes" { return $true }
        "y" { return $true }
        "0" { return $false }
        "false" { return $false }
        "no" { return $false }
        "n" { return $false }
        default { throw "Valor invalido para '$Name': $Value. Use true/false ou 1/0." }
    }
}

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

function Assert-PublishOutput {
    param(
        [string]$PublishPath,
        [bool]$SelfContainedRequested
    )

    if (-not (Test-Path $PublishPath)) {
        throw "Pasta de publish nao encontrada: $PublishPath"
    }

    if (-not $SelfContainedRequested) {
        return
    }

    $requiredFiles = @(
        "DDSStudyOS.App.exe",
        "hostfxr.dll",
        "hostpolicy.dll",
        "coreclr.dll"
    )

    foreach ($required in $requiredFiles) {
        $fullPath = Join-Path $PublishPath $required
        if (-not (Test-Path $fullPath)) {
            throw "Publish invalido: arquivo obrigatorio de self-contained nao encontrado: $fullPath"
        }
    }

    $runtimeConfigPath = Join-Path $PublishPath "DDSStudyOS.App.runtimeconfig.json"
    if (-not (Test-Path $runtimeConfigPath)) {
        throw "Publish invalido: runtimeconfig nao encontrado: $runtimeConfigPath"
    }

    $runtimeConfig = Get-Content -Path $runtimeConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $includedFrameworks = $runtimeConfig.runtimeOptions.includedFrameworks
    if (-not $includedFrameworks -or $includedFrameworks.Count -eq 0) {
        throw "Publish invalido: expected 'includedFrameworks' para build self-contained em $runtimeConfigPath."
    }
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
$selfContainedValue = ConvertTo-BoolValue -Value $SelfContained -Name "SelfContained"
$windowsAppSdkSelfContainedValue = ConvertTo-BoolValue -Value $WindowsAppSDKSelfContained -Name "WindowsAppSDKSelfContained"
Write-Host "MSBuild: $resolvedMsBuild"
Write-Host "Configuration: $Configuration"
Write-Host "Platform: $Platform"
Write-Host "RuntimeIdentifier: $RuntimeIdentifier"
Write-Host "SelfContained: $selfContainedValue"
Write-Host "WindowsAppSDKSelfContained: $windowsAppSdkSelfContainedValue"

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
& $resolvedMsBuild $projectPath /t:Publish /p:Configuration=$Configuration /p:Platform=$Platform /p:RuntimeIdentifier=$RuntimeIdentifier /p:SelfContained=$selfContainedValue /p:WindowsAppSDKSelfContained=$windowsAppSdkSelfContainedValue /m /nologo
if ($LASTEXITCODE -ne 0) {
    throw "Falha no publish do app."
}

$publishPath = Join-Path $repoRoot "src\DDSStudyOS.App\bin\$Platform\$Configuration\net8.0-windows10.0.19041.0\$RuntimeIdentifier\publish"
Assert-PublishOutput -PublishPath $publishPath -SelfContainedRequested $selfContainedValue
Write-Host ""
Write-Host "Publish concluido em: $publishPath"
Write-Host "Observacao: o publish recria o EXE. Assine novamente com scripts/sign-release.ps1."
