param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$GitHubOwner = "",
    [string]$GitHubRepo = "",
    [string]$SelfContained = "true",
    [string]$WindowsAppSDKSelfContained = "true",
    [string]$PrepareInput = "true",
    [string]$InstallerInputPath = "artifacts\installer-input\app",
    [string]$OutputPath = "artifacts\installer-output",
    [string]$SetupBaseName = "DDSStudyOS-Setup",
    [string]$InstallWebView2 = "true",
    [string]$InstallDotNetDesktopRuntime = "false",
    [int]$DotNetDesktopRuntimeMajor = 8,
    [string]$InnoCompilerPath = "",
    [string]$ScriptPath = "installer\inno\DDSStudyOS.iss",
    [string]$BrandingSourceImage = "src\DDSStudyOS.App\Assets\SplashBackground.png",
    [string]$BrandingOutputDir = "installer\inno\branding",
    [string]$GenerateBranding = "true",
    [switch]$SignExecutable,
    [switch]$SignInstaller,
    [string]$CertThumbprint = "6780CE530A33615B591727F5334B3DD075B76422",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [ValidateSet("CurrentUser", "LocalMachine")]
    [string]$CertStoreScope = "CurrentUser",
    [string]$TimestampUrl = ""
)

$ErrorActionPreference = "Stop"
$repoLinksScript = Join-Path $PSScriptRoot "repo-links.ps1"
if (-not (Test-Path $repoLinksScript)) {
    throw "Script de links do repositorio nao encontrado: $repoLinksScript"
}
. $repoLinksScript

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

function Resolve-InnoCompilerPath {
    param([string]$PathOverride)

    if (-not [string]::IsNullOrWhiteSpace($PathOverride)) {
        if (-not (Test-Path $PathOverride)) {
            throw "ISCC.exe nao encontrado no caminho informado: $PathOverride"
        }
        return (Resolve-Path $PathOverride).Path
    }

    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "F:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "F:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    throw "Inno Setup Compiler (ISCC.exe) nao encontrado."
}

function Resolve-ProductVersion {
    param([string]$RepoRoot)

    $projectFile = Join-Path $RepoRoot "src\DDSStudyOS.App\DDSStudyOS.App.csproj"
    if (-not (Test-Path $projectFile)) {
        throw "Projeto nao encontrado para resolver versao: $projectFile"
    }

    [xml]$xml = Get-Content $projectFile -Raw -Encoding UTF8
    $raw = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($raw)) {
        throw "Tag <Version> nao encontrada em: $projectFile"
    }

    return ($raw -split '[\+\-]')[0]
}

function ConvertTo-RelativePath {
    param(
        [string]$BasePath,
        [string]$TargetPath
    )

    $baseFullPath = [System.IO.Path]::GetFullPath($BasePath)
    if (-not $baseFullPath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $baseFullPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $targetFullPath = [System.IO.Path]::GetFullPath($TargetPath)
    $baseUri = New-Object System.Uri($baseFullPath)
    $targetUri = New-Object System.Uri($targetFullPath)
    $relativeUri = $baseUri.MakeRelativeUri($targetUri)
    $relativePath = [System.Uri]::UnescapeDataString($relativeUri.ToString()).Replace('/', '\')

    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        return "."
    }

    return $relativePath
}

function Invoke-PowerShellScript {
    param(
        [string[]]$Arguments,
        [string]$ErrorMessage
    )

    & powershell.exe @Arguments
    $exitCode = if ($null -eq $LASTEXITCODE) { 0 } else { [int]$LASTEXITCODE }
    if ($exitCode -ne 0) {
        throw $ErrorMessage
    }
}

function Resolve-GeneratedSetupPath {
    param(
        [string]$OutputDirectory,
        [string]$ExpectedSetupPath,
        [int]$MaxAttempts = 20,
        [int]$DelayMilliseconds = 500
    )

    if ($MaxAttempts -lt 1) { $MaxAttempts = 1 }
    if ($DelayMilliseconds -lt 0) { $DelayMilliseconds = 0 }

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        if (Test-Path $ExpectedSetupPath) {
            return $ExpectedSetupPath
        }

        Start-Sleep -Milliseconds $DelayMilliseconds
    }

    return $null
}

function Wait-InnoCompilerCompletion {
    param(
        [string]$SetupBaseName,
        [int]$TimeoutSeconds = 300,
        [int]$PollMilliseconds = 1000
    )

    if ([string]::IsNullOrWhiteSpace($SetupBaseName)) {
        return
    }

    if ($TimeoutSeconds -lt 1) { $TimeoutSeconds = 1 }
    if ($PollMilliseconds -lt 100) { $PollMilliseconds = 100 }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $argToken = "/DMySetupBaseName=$SetupBaseName"

    while ((Get-Date) -lt $deadline) {
        $processes = Get-CimInstance Win32_Process -Filter "Name='ISCC.exe'" -ErrorAction SilentlyContinue |
            Where-Object {
                $_.CommandLine -and $_.CommandLine.IndexOf($argToken, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
            }

        if (-not $processes) {
            return
        }

        Start-Sleep -Milliseconds $PollMilliseconds
    }

    throw "Timeout aguardando compiladores ISCC finalizarem para '$SetupBaseName'."
}

function Assert-InstallerInputContainsApp {
    param(
        [string]$InputPath
    )

    $requiredFiles = @(
        "DDSStudyOS.App.exe",
        "DDSStudyOS.App.dll",
        "DDSStudyOS.App.deps.json",
        "DDSStudyOS.App.runtimeconfig.json",
        "DDSStudyOS.App.pri"
    )

    $missing = @()
    foreach ($file in $requiredFiles) {
        $fullPath = Join-Path $InputPath $file
        if (-not (Test-Path $fullPath)) {
            $missing += $file
        }
    }

    if ($missing.Count -gt 0) {
        throw "Input do instalador incompleto. Arquivos obrigatorios ausentes em '$InputPath': $($missing -join ', ')"
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$repoLinks = Get-DdsRepoLinks -RepoRoot $repoRoot -Owner $GitHubOwner -Repo $GitHubRepo
$resolvedScriptPath = if ([System.IO.Path]::IsPathRooted($ScriptPath)) { $ScriptPath } else { Join-Path $repoRoot $ScriptPath }
$resolvedInputPath = if ([System.IO.Path]::IsPathRooted($InstallerInputPath)) { $InstallerInputPath } else { Join-Path $repoRoot $InstallerInputPath }
$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
$scriptDirectory = Split-Path -Path $resolvedScriptPath -Parent
$isccScriptPath = ConvertTo-RelativePath -BasePath $repoRoot -TargetPath $resolvedScriptPath
$isccSourceDir = ConvertTo-RelativePath -BasePath $scriptDirectory -TargetPath $resolvedInputPath
$isccOutputDir = ConvertTo-RelativePath -BasePath $scriptDirectory -TargetPath $resolvedOutputPath

if (-not (Test-Path $resolvedScriptPath)) {
    throw "Script do Inno Setup nao encontrado: $resolvedScriptPath"
}

$prepareInputValue = ConvertTo-BoolValue -Value $PrepareInput -Name "PrepareInput"
$selfContainedValue = ConvertTo-BoolValue -Value $SelfContained -Name "SelfContained"
$windowsAppSdkSelfContainedValue = ConvertTo-BoolValue -Value $WindowsAppSDKSelfContained -Name "WindowsAppSDKSelfContained"
$installWebView2Value = ConvertTo-BoolValue -Value $InstallWebView2 -Name "InstallWebView2"
$installDotNetDesktopRuntimeValue = ConvertTo-BoolValue -Value $InstallDotNetDesktopRuntime -Name "InstallDotNetDesktopRuntime"
$generateBrandingValue = ConvertTo-BoolValue -Value $GenerateBranding -Name "GenerateBranding"

if ($generateBrandingValue) {
    $brandingScript = Join-Path $PSScriptRoot "generate-inno-branding.ps1"
    if (-not (Test-Path $brandingScript)) {
        throw "Script de branding nao encontrado: $brandingScript"
    }

    Write-Host "==> Gerando branding do instalador Inno"
    $brandingArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $brandingScript,
        "-SourceImage", $BrandingSourceImage,
        "-OutputDir", $BrandingOutputDir,
        "-Force"
    )
    Invoke-PowerShellScript -Arguments $brandingArgs -ErrorMessage "Falha ao gerar branding do instalador Inno."
}

if ($prepareInputValue) {
    $prepareScript = Join-Path $PSScriptRoot "prepare-installer-input.ps1"
    if (-not (Test-Path $prepareScript)) {
        throw "Script nao encontrado: $prepareScript"
    }

    $selfContainedArg = if ($selfContainedValue) { "1" } else { "0" }
    $windowsAppSdkSelfContainedArg = if ($windowsAppSdkSelfContainedValue) { "1" } else { "0" }

    $prepareArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $prepareScript,
        "-Configuration", $Configuration,
        "-Platform", $Platform,
        "-RuntimeIdentifier", $RuntimeIdentifier,
        "-GitHubOwner", $repoLinks.Owner,
        "-GitHubRepo", $repoLinks.Repo,
        "-SelfContained", $selfContainedArg,
        "-WindowsAppSDKSelfContained", $windowsAppSdkSelfContainedArg
    )

    if ($SignExecutable) {
        $prepareArgs += "-SignExecutable"
        if (-not [string]::IsNullOrWhiteSpace($CertThumbprint)) {
            $prepareArgs += @("-CertThumbprint", $CertThumbprint)
        }
        if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
            $prepareArgs += @("-PfxPath", $PfxPath, "-PfxPassword", $PfxPassword)
        }
        if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
            $prepareArgs += @("-TimestampUrl", $TimestampUrl)
        }
        $prepareArgs += @("-CertStoreScope", $CertStoreScope)
    }

    Write-Host "==> Preparando arquivos para o instalador"
    Invoke-PowerShellScript -Arguments $prepareArgs -ErrorMessage "Falha ao preparar arquivos de entrada."
}

if (-not (Test-Path $resolvedInputPath)) {
    throw "Pasta de entrada nao encontrada: $resolvedInputPath"
}

Assert-InstallerInputContainsApp -InputPath $resolvedInputPath

if (-not (Test-Path $resolvedOutputPath)) {
    New-Item -ItemType Directory -Path $resolvedOutputPath -Force | Out-Null
}

$iscc = Resolve-InnoCompilerPath -PathOverride $InnoCompilerPath
$version = Resolve-ProductVersion -RepoRoot $repoRoot

Write-Host "==> Compilando instalador oficial"
Write-Host "ISCC: $iscc"
Write-Host "Versao: $version"
Write-Host "Repo URL: $($repoLinks.BaseUrl)"
Write-Host "Entrada: $resolvedInputPath"
Write-Host "Saida: $resolvedOutputPath"
Write-Host "Prereq WebView2: $installWebView2Value"
Write-Host "Prereq .NET Desktop Runtime: $installDotNetDesktopRuntimeValue (major $DotNetDesktopRuntimeMajor)"

$isccArgs = @(
    "/Qp",
    "/DMyAppVersion=$version",
    "/DMySourceDir=$isccSourceDir",
    "/DMyOutputDir=$isccOutputDir",
    "/DMySetupBaseName=$SetupBaseName",
    "/DMyAppURL=$($repoLinks.BaseUrl)",
    "/DMyInstallWebView2=$([int]$installWebView2Value)",
    "/DMyInstallDotNetDesktopRuntime=$([int]$installDotNetDesktopRuntimeValue)",
    "/DMyDotNetDesktopRuntimeMajor=$DotNetDesktopRuntimeMajor",
    $isccScriptPath
)

$setupPath = Join-Path $resolvedOutputPath "$SetupBaseName.exe"
if (Test-Path $setupPath) {
    try {
        Remove-Item $setupPath -Force -ErrorAction Stop
    }
    catch {
        # Mantem melhor esforco: se o arquivo estiver bloqueado, o ISCC pode sobrescrever.
    }
}

$isccProcess = Start-Process -FilePath $iscc -ArgumentList $isccArgs -WorkingDirectory $repoRoot -NoNewWindow -Wait -PassThru
if ($isccProcess.ExitCode -ne 0) {
    throw "Falha ao compilar instalador oficial."
}

Wait-InnoCompilerCompletion -SetupBaseName $SetupBaseName -TimeoutSeconds 300 -PollMilliseconds 1000
$setupPath = Resolve-GeneratedSetupPath -OutputDirectory $resolvedOutputPath -ExpectedSetupPath $setupPath

if ([string]::IsNullOrWhiteSpace($setupPath) -or -not (Test-Path $setupPath)) {
    throw "Compilacao concluida, mas setup nao encontrado em: $resolvedOutputPath"
}

if ($SignInstaller) {
    $signScript = Join-Path $PSScriptRoot "sign-release.ps1"
    if (-not (Test-Path $signScript)) {
        throw "Script de assinatura nao encontrado: $signScript"
    }

    Write-Host "==> Assinando instalador"
    $signArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $signScript,
        "-TargetPaths", $setupPath
    )

    if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
        $signArgs += @("-TimestampUrl", $TimestampUrl)
    }

    if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
        $signArgs += @("-PfxPath", $PfxPath, "-PfxPassword", $PfxPassword)
    }
    else {
        if ([string]::IsNullOrWhiteSpace($CertThumbprint)) {
            throw "Para assinar o instalador, informe -CertThumbprint ou -PfxPath."
        }
        $signArgs += @("-CertThumbprint", $CertThumbprint, "-CertStoreScope", $CertStoreScope)
    }

    Invoke-PowerShellScript -Arguments $signArgs -ErrorMessage "Falha ao assinar instalador: $setupPath"
}

Write-Host ""
Write-Host "Setup oficial gerado com sucesso:"
Write-Host $setupPath




