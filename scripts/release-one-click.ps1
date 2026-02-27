param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputPath = "artifacts/installer-output",
    [string]$DlcOutputPath = "artifacts/dlc-output",
    [string]$LogDirectory = "artifacts/installer-logs",
    [string]$GateOutputDirectory = "artifacts/release-gate",
    [string]$StableSetupBaseName = "DDSStudyOS-Setup",
    [string]$BetaSetupBaseName = "DDSStudyOS-Beta-Setup",
    [string]$PortableBaseName = "DDSStudyOS-Portable",
    [string]$ShaFileName = "DDSStudyOS-SHA256.txt",
    [string]$Owner = "Erikalellis",
    [string]$Repo = "DDSStudyOS",
    [string]$ReleaseTag = "",
    [switch]$SkipFirstUseSmoke,
    [switch]$SkipCleanMachineSmoke,
    [switch]$KeepInstalled,
    [switch]$AllowSkippedChecks
)

$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [string]$RepoRoot,
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $RepoRoot $Path
}

function Resolve-AppVersion {
    param([string]$RepoRoot)

    $projectFile = Join-Path $RepoRoot "src\DDSStudyOS.App\DDSStudyOS.App.csproj"
    if (-not (Test-Path $projectFile)) {
        throw "Projeto nao encontrado: $projectFile"
    }

    [xml]$xml = Get-Content $projectFile -Raw -Encoding UTF8
    $rawVersion = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($rawVersion)) {
        throw "Tag <Version> nao encontrada em: $projectFile"
    }

    return ($rawVersion -split '[\+\-]')[0]
}

function Get-LatestFileAfter {
    param(
        [string]$DirectoryPath,
        [string]$Pattern,
        [datetime]$After
    )

    if (-not (Test-Path $DirectoryPath)) {
        return $null
    }

    return Get-ChildItem -Path $DirectoryPath -Filter $Pattern -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -ge $After } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

function Test-SmokeReport {
    param(
        [string]$ReportPath,
        [string]$SuccessMarker
    )

    if (-not (Test-Path $ReportPath)) {
        return [pscustomobject]@{
            Passed = $false
            Message = "Relatorio nao encontrado: $ReportPath"
        }
    }

    $content = Get-Content $ReportPath -Raw -Encoding UTF8
    if ($content -match "\[FAIL\]") {
        return [pscustomobject]@{
            Passed = $false
            Message = "Relatorio possui marcador [FAIL]: $ReportPath"
        }
    }

    if ($content -notmatch [regex]::Escape($SuccessMarker)) {
        return [pscustomobject]@{
            Passed = $false
            Message = "Marcador de sucesso ausente ($SuccessMarker): $ReportPath"
        }
    }

    return [pscustomobject]@{
        Passed = $true
        Message = "OK: $ReportPath"
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedOutputPath = Resolve-AbsolutePath -RepoRoot $repoRoot -Path $OutputPath
$resolvedLogDirectory = Resolve-AbsolutePath -RepoRoot $repoRoot -Path $LogDirectory
$resolvedGateOutputDir = Resolve-AbsolutePath -RepoRoot $repoRoot -Path $GateOutputDirectory

if (-not (Test-Path $resolvedGateOutputDir)) {
    New-Item -ItemType Directory -Path $resolvedGateOutputDir -Force | Out-Null
}

$releaseScript = Join-Path $PSScriptRoot "build-release-package.ps1"
$dlcScript = Join-Path $PSScriptRoot "build-dlc-package.ps1"
$firstUseScript = Join-Path $PSScriptRoot "validate-first-use-smoke.ps1"
$cleanMachineScript = Join-Path $PSScriptRoot "validate-clean-machine-smoke.ps1"

foreach ($requiredScript in @($releaseScript, $dlcScript, $firstUseScript, $cleanMachineScript)) {
    if (-not (Test-Path $requiredScript)) {
        throw "Script obrigatorio nao encontrado: $requiredScript"
    }
}

$stableVersion = Resolve-AppVersion -RepoRoot $repoRoot
$effectiveBetaVersion = "$stableVersion-beta.1"
$releaseStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $resolvedGateOutputDir "release-gate-$releaseStamp.md"

$checks = New-Object System.Collections.Generic.List[object]

function Add-Check {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Details
    )

    $checks.Add([pscustomobject]@{
        Name = $Name
        Passed = $Passed
        Details = $Details
    }) | Out-Null
}

Write-Host "==> Release one-click"
Write-Host "Versao estavel: $stableVersion"
Write-Host "Versao beta alvo: $effectiveBetaVersion"

Write-Host ""
Write-Host "==> 1/4 Build release package (setup + beta + portable + sha256)"
& $releaseScript `
    -Configuration $Configuration `
    -Platform $Platform `
    -RuntimeIdentifier $RuntimeIdentifier `
    -OutputPath $OutputPath `
    -StableSetupBaseName $StableSetupBaseName `
    -BetaSetupBaseName $BetaSetupBaseName `
    -PortableBaseName $PortableBaseName `
    -ShaFileName $ShaFileName `
    -GitHubOwner $Owner `
    -GitHubRepo $Repo

Write-Host ""
Write-Host "==> 2/4 Build DLC manifests (stable + beta)"
& $dlcScript -Channel stable -OutputPath $DlcOutputPath -Owner $Owner -Repo $Repo -ReleaseTag $ReleaseTag
& $dlcScript -Channel beta -OutputPath $DlcOutputPath -Owner $Owner -Repo $Repo -ReleaseTag $ReleaseTag

$stableSetupPath = Join-Path $resolvedOutputPath "$StableSetupBaseName.exe"
$betaSetupPath = Join-Path $resolvedOutputPath "$BetaSetupBaseName.exe"
$portablePath = Join-Path $resolvedOutputPath "$PortableBaseName.zip"
$shaPath = Join-Path $resolvedOutputPath $ShaFileName

Add-Check -Name "Setup estavel gerado" -Passed:(Test-Path $stableSetupPath) -Details $stableSetupPath
Add-Check -Name "Setup beta gerado" -Passed:(Test-Path $betaSetupPath) -Details $betaSetupPath
Add-Check -Name "Portable zip gerado" -Passed:(Test-Path $portablePath) -Details $portablePath
Add-Check -Name "SHA256 gerado" -Passed:(Test-Path $shaPath) -Details $shaPath

$stableUpdateInfoPath = Join-Path $repoRoot "installer\update\stable\update-info.json"
$betaUpdateInfoPath = Join-Path $repoRoot "installer\update\beta\update-info.json"
$stableUpdateInfoOk = $false
$betaUpdateInfoOk = $false
$stableUpdateInfoDetails = $stableUpdateInfoPath
$betaUpdateInfoDetails = $betaUpdateInfoPath

if (Test-Path $stableUpdateInfoPath) {
    try {
        $stableJson = Get-Content $stableUpdateInfoPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $stableUpdateInfoOk = [string]::Equals([string]$stableJson.currentVersion, $stableVersion, [System.StringComparison]::OrdinalIgnoreCase)
        $stableUpdateInfoDetails = "currentVersion=$($stableJson.currentVersion) (esperado: $stableVersion)"
    }
    catch {
        $stableUpdateInfoDetails = "Falha ao ler JSON: $($_.Exception.Message)"
    }
}

if (Test-Path $betaUpdateInfoPath) {
    try {
        $betaJson = Get-Content $betaUpdateInfoPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $betaUpdateInfoOk = ([string]$betaJson.currentVersion).StartsWith("$stableVersion-beta", [System.StringComparison]::OrdinalIgnoreCase)
        $betaUpdateInfoDetails = "currentVersion=$($betaJson.currentVersion) (esperado prefixo: $stableVersion-beta)"
    }
    catch {
        $betaUpdateInfoDetails = "Falha ao ler JSON: $($_.Exception.Message)"
    }
}

Add-Check -Name "update-info stable sincronizado" -Passed:$stableUpdateInfoOk -Details $stableUpdateInfoDetails
Add-Check -Name "update-info beta sincronizado" -Passed:$betaUpdateInfoOk -Details $betaUpdateInfoDetails

$stableDlcManifestPath = Join-Path $repoRoot "installer\update\stable\dlc-manifest.json"
$betaDlcManifestPath = Join-Path $repoRoot "installer\update\beta\dlc-manifest.json"

foreach ($manifestPath in @($stableDlcManifestPath, $betaDlcManifestPath)) {
    $manifestOk = $false
    $manifestDetails = $manifestPath

    if (Test-Path $manifestPath) {
        try {
            $manifestJson = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $moduleCount = @($manifestJson.modules).Count
            $manifestOk = $moduleCount -ge 0
            $manifestDetails = "modulos=$moduleCount | $manifestPath"
        }
        catch {
            $manifestDetails = "Falha ao ler JSON: $($_.Exception.Message)"
        }
    }

    $manifestName = if ($manifestPath -like "*\\stable\\*") { "DLC manifest stable gerado" } else { "DLC manifest beta gerado" }
    Add-Check -Name $manifestName -Passed:$manifestOk -Details $manifestDetails
}

$firstUseReport = $null
$cleanMachineReport = $null

if ($SkipFirstUseSmoke) {
    Add-Check -Name "Smoke first-use executado" -Passed:$AllowSkippedChecks.IsPresent -Details "SKIPPED via -SkipFirstUseSmoke"
}
else {
    Write-Host ""
    Write-Host "==> 3/4 Smoke first-use"
    $beforeSmoke = Get-Date
    & $firstUseScript -LogDirectory $LogDirectory
    $firstUseReport = Get-LatestFileAfter -DirectoryPath $resolvedLogDirectory -Pattern "first-use-smoke-*.txt" -After $beforeSmoke
    $firstUseResult = if ($null -eq $firstUseReport) {
        [pscustomobject]@{ Passed = $false; Message = "Relatorio first-use nao encontrado em $resolvedLogDirectory" }
    }
    else {
        Test-SmokeReport -ReportPath $firstUseReport.FullName -SuccessMarker "[OK] First-use smoke validado com sucesso."
    }

    Add-Check -Name "Smoke first-use executado" -Passed:$firstUseResult.Passed -Details $firstUseResult.Message
}

if ($SkipCleanMachineSmoke) {
    Add-Check -Name "Smoke clean-machine executado" -Passed:$AllowSkippedChecks.IsPresent -Details "SKIPPED via -SkipCleanMachineSmoke"
}
else {
    Write-Host ""
    Write-Host "==> 4/4 Smoke clean-machine"
    $beforeCleanSmoke = Get-Date
    & $cleanMachineScript `
        -SetupPath (Join-Path $OutputPath "$StableSetupBaseName.exe") `
        -LogDirectory $LogDirectory `
        -RunSetup `
        -KeepInstalled:$KeepInstalled

    $cleanMachineReport = Get-LatestFileAfter -DirectoryPath $resolvedLogDirectory -Pattern "clean-machine-smoke-*.txt" -After $beforeCleanSmoke
    $cleanMachineResult = if ($null -eq $cleanMachineReport) {
        [pscustomobject]@{ Passed = $false; Message = "Relatorio clean-machine nao encontrado em $resolvedLogDirectory" }
    }
    else {
        Test-SmokeReport -ReportPath $cleanMachineReport.FullName -SuccessMarker "[OK] Validacao finalizada."
    }

    Add-Check -Name "Smoke clean-machine executado" -Passed:$cleanMachineResult.Passed -Details $cleanMachineResult.Message
}

$reportLines = @()
$firstUseReportPath = if ($null -ne $firstUseReport) { $firstUseReport.FullName } else { "(nao executado)" }
$cleanMachineReportPath = if ($null -ne $cleanMachineReport) { $cleanMachineReport.FullName } else { "(nao executado)" }
$reportLines += "# DDS StudyOS - Gate automatico (one-click)"
$reportLines += ""
$reportLines += "- Data: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$reportLines += "- Versao estavel: $stableVersion"
$reportLines += "- Versao beta alvo: $effectiveBetaVersion"
$reportLines += ""
$reportLines += "## Resultado dos checks"

foreach ($check in $checks) {
    $status = if ($check.Passed) { "[x]" } else { "[ ]" }
    $reportLines += "- $status $($check.Name) - $($check.Details)"
}

$reportLines += ""
$reportLines += "## Evidencias desta execucao"
$reportLines += "- Setup estavel: $stableSetupPath"
$reportLines += "- Setup beta: $betaSetupPath"
$reportLines += "- Portable: $portablePath"
$reportLines += "- SHA256: $shaPath"
$reportLines += "- DLC stable manifest: $stableDlcManifestPath"
$reportLines += "- DLC beta manifest: $betaDlcManifestPath"
$reportLines += "- First-use smoke: $firstUseReportPath"
$reportLines += "- Clean-machine smoke: $cleanMachineReportPath"

$reportLines | Set-Content -Path $reportPath -Encoding UTF8

$failedChecks = @($checks | Where-Object { -not $_.Passed })
if ($failedChecks.Count -gt 0) {
    Write-Host ""
    Write-Host "Gate automatico: FAIL"
    Write-Host "Relatorio: $reportPath"
    exit 2
}

Write-Host ""
Write-Host "Gate automatico: GO"
Write-Host "Relatorio: $reportPath"
