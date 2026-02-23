param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$RuntimeIdentifier = "win-x64",
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

    return $null
}

function Resolve-DotnetPath {
    $candidates = @(
        "C:\Program Files\dotnet\dotnet.exe",
        "F:\Program Files\dotnet\dotnet.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    throw "dotnet nao encontrado. Instale o SDK .NET 8."
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

    $priPath = Join-Path $PublishPath "DDSStudyOS.App.pri"
    if (-not (Test-Path $priPath)) {
        throw "Publish invalido: arquivo .pri do app nao encontrado: $priPath"
    }

    $xbfFiles = Get-ChildItem -Path $PublishPath -Filter "*.xbf" -Recurse -File -ErrorAction SilentlyContinue
    if (-not $xbfFiles -or $xbfFiles.Count -eq 0) {
        throw "Publish invalido: nenhum arquivo .xbf foi encontrado em: $PublishPath"
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

function Repair-WinUIUnpackagedPublishArtifacts {
    param(
        [string]$BuildOutputPath,
        [string]$PublishPath
    )

    if (-not (Test-Path $BuildOutputPath)) {
        throw "Pasta de build nao encontrada: $BuildOutputPath"
    }
    if (-not (Test-Path $PublishPath)) {
        throw "Pasta de publish nao encontrada: $PublishPath"
    }

    # Bug fix: uma versao anterior deste script podia acabar copiando artefatos do proprio
    # publish (BuildOutputPath\\publish) para dentro do publish novamente, criando
    # pastas aninhadas do tipo publish\\publish. Remova isso para nao poluir o instalador.
    $nestedPublish = Join-Path $PublishPath "publish"
    if (Test-Path $nestedPublish) {
        $nestedXbfs = Get-ChildItem -Path $nestedPublish -Filter "*.xbf" -Recurse -File -ErrorAction SilentlyContinue
        if ($nestedXbfs -and $nestedXbfs.Count -gt 0) {
            Write-Host "==> Limpando pasta aninhada indevida: $nestedPublish"
            Remove-Item -Path $nestedPublish -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    # Em projetos WinUI 3 unpackaged, alguns artefatos gerados (XBF/PRI do app) nem sempre
    # sao copiados automaticamente para a pasta de publish. Sem eles, InitializeComponent falha.
    $appPriName = "DDSStudyOS.App.pri"
    $publishPri = Join-Path $PublishPath $appPriName
    $buildPri = Join-Path $BuildOutputPath $appPriName
    if (Test-Path $buildPri) {
        Write-Host "==> Corrigindo publish: copiando $appPriName"
        Copy-Item -Path $buildPri -Destination $publishPri -Force
    }

    # Nao copie nada que ja esteja dentro de BuildOutputPath\\publish (evita publish\\publish)
    $excludeDir = Join-Path $BuildOutputPath "publish"
    $excludePrefix = $null
    if (Test-Path $excludeDir) {
        $excludePrefix = (Resolve-Path $excludeDir).Path.TrimEnd('\') + '\'
    }

    $buildXbfs = Get-ChildItem -Path $BuildOutputPath -Filter "*.xbf" -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            if (-not $excludePrefix) { return $true }
            return -not $_.FullName.StartsWith($excludePrefix, [System.StringComparison]::OrdinalIgnoreCase)
        }
    if ($buildXbfs -and $buildXbfs.Count -gt 0) {
        Write-Host "==> Corrigindo publish: copiando arquivos .xbf ($($buildXbfs.Count))"
        foreach ($file in $buildXbfs) {
            $relative = $file.FullName.Substring($BuildOutputPath.Length).TrimStart('\')
            $dest = Join-Path $PublishPath $relative
            $destDir = Split-Path -Path $dest -Parent
            if (-not (Test-Path $destDir)) {
                New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            }
            Copy-Item -Path $file.FullName -Destination $dest -Force
        }
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
$resolvedDotnet = Resolve-DotnetPath
$selfContainedValue = ConvertTo-BoolValue -Value $SelfContained -Name "SelfContained"
$windowsAppSdkSelfContainedValue = ConvertTo-BoolValue -Value $WindowsAppSDKSelfContained -Name "WindowsAppSDKSelfContained"
if ($resolvedMsBuild) {
    Write-Host "MSBuild: $resolvedMsBuild"
}
else {
    Write-Host "MSBuild: nao encontrado (fallback dotnet publish)"
}
Write-Host "dotnet: $resolvedDotnet"
Write-Host "Configuration: $Configuration"
Write-Host "Platform: $Platform"
Write-Host "RuntimeIdentifier: $RuntimeIdentifier"
Write-Host "SelfContained: $selfContainedValue"
Write-Host "WindowsAppSDKSelfContained: $windowsAppSdkSelfContainedValue"

$effectiveSkipSolutionBuild = [bool]$SkipSolutionBuild
if (-not $effectiveSkipSolutionBuild -and $windowsAppSdkSelfContainedValue) {
    Write-Host ""
    Write-Host "==> Build da solution ignorado"
    Write-Host "Motivo: evitar divergencia de configuracao entre solution e publish no modo WindowsAppSDKSelfContained."
    Write-Host "O publish do projeto sera executado diretamente com RuntimeIdentifier/Platform explicitos."
    $effectiveSkipSolutionBuild = $true
}

if (-not $effectiveSkipSolutionBuild) {
    Write-Host ""
    Write-Host "==> Build da solution"

    if ($resolvedMsBuild) {
        & $resolvedMsBuild $solutionPath /t:Build /p:Configuration=$Configuration /m /nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Falha no build da solution."
        }
    }
    else {
        $buildArgs = "build `"$solutionPath`" -c $Configuration -p:Platform=$Platform --tl:off -v minimal"
        $buildProc = Start-Process -FilePath $resolvedDotnet -ArgumentList $buildArgs -NoNewWindow -Wait -PassThru
        if ($buildProc.ExitCode -ne 0) {
            throw "Falha no build da solution (dotnet)."
        }
    }
}

Write-Host ""
Write-Host "==> Publish do app"
if ($resolvedMsBuild) {
    & $resolvedMsBuild $projectPath /t:Publish /p:Configuration=$Configuration /p:Platform=$Platform /p:RuntimeIdentifier=$RuntimeIdentifier /p:SelfContained=$selfContainedValue /p:WindowsAppSDKSelfContained=$windowsAppSdkSelfContainedValue /m /nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Falha no publish do app."
    }
}
else {
    $publishArgs = "publish `"$projectPath`" -c $Configuration -r $RuntimeIdentifier -p:Platform=$Platform -p:SelfContained=$selfContainedValue -p:WindowsAppSDKSelfContained=$windowsAppSdkSelfContainedValue --tl:off -v minimal"
    $publishProc = Start-Process -FilePath $resolvedDotnet -ArgumentList $publishArgs -NoNewWindow -Wait -PassThru
    if ($publishProc.ExitCode -ne 0) {
        throw "Falha no publish do app (dotnet)."
    }
}

$buildOutputPath = Join-Path $repoRoot "src\DDSStudyOS.App\bin\$Platform\$Configuration\net8.0-windows10.0.19041.0\$RuntimeIdentifier"
$publishPath = Join-Path $buildOutputPath "publish"

Repair-WinUIUnpackagedPublishArtifacts -BuildOutputPath $buildOutputPath -PublishPath $publishPath
Assert-PublishOutput -PublishPath $publishPath -SelfContainedRequested $selfContainedValue
Write-Host ""
Write-Host "Publish concluido em: $publishPath"
Write-Host "Observacao: o publish recria o EXE. Assine novamente com scripts/sign-release.ps1."

